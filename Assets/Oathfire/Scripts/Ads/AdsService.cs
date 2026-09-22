using System;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;

#if UNITY_ANDROID && !UNITY_EDITOR
using GoogleMobileAds.Api;
#endif

namespace Oathfire.Ads
{
    /// <summary>
    /// The Warden's ledger of sponsored favour. AdMob is wired with Google's public test units; swap the three
    /// unit ids below for the real placements when the AdMob account exists. Off Android every call is a quiet
    /// no-op (rewarded grants immediately) so the game never blocks on ads it cannot serve.
    /// Placements:
    ///   banner       — bottom of the title screen only; never inside play.
    ///   interstitial — quiet beats only: dawn after a held night, a fallen boss, a main quest handed in,
    ///                  every fourth equipment/journal close, Return to Title, after the ending cinematic.
    ///                  One global cooldown plus a session cap governs them all.
    ///   rewarded     — opt-in only: keep the fallen Warden's coin (defeat screen) and the benefactor's
    ///                  coin gift in the pause menu (once per cool-off).
    /// A single non-consumable purchase ("remove_ads") silences every placement forever; the entitlement
    /// persists in PlayerPrefs and is restored from the store receipt on reinstall.
    /// </summary>
    public class AdsService : MonoBehaviour, IDetailedStoreListener
    {
        /// <summary>Store product id — must match the Play Console in-app product.</summary>
        public const string RemoveAdsProduct = "remove_ads";
        const string RemovedPref = "oathfire.adsRemoved";

#if UNITY_ANDROID && !UNITY_EDITOR
        const string BannerUnit = "ca-app-pub-3940256099942544/6300978111";
        const string InterstitialUnit = "ca-app-pub-3940256099942544/1033173712";
        const string RewardedUnit = "ca-app-pub-3940256099942544/5224354917";
#else
        const string BannerUnit = "";
        const string InterstitialUnit = "";
        const string RewardedUnit = "";
#endif

        /// <summary>Benefactor's gift cool-off in minutes; also caps interstitials per session.</summary>
        const float GiftCooldownMinutes = 10f;
        const int MaxInterstitialsPerSession = 4;
        const float InterstitialCooldownMinutes = 10f;
        const int MenuOpensPerAd = 4;

#if UNITY_ANDROID && !UNITY_EDITOR
        BannerView banner;
        InterstitialAd interstitial;
        RewardedAd rewarded;
#endif
        IStoreController store;
        IExtensionProvider extensions;
        bool initializing;
        float lastGiftTime = float.MinValue;
        float lastInterstitialTime = float.MinValue;
        int interstitialsShown;
        int menuOpens;

        /// <summary>Raised on the main thread the moment the entitlement lands (purchase or restore).</summary>
        public event Action AdsRemovedChanged;

        /// <summary>True once the remove-ads purchase is owned. Silences every placement.</summary>
        public bool AdsRemoved { get; private set; }

        /// <summary>Store front-end for the About sheet: true once Unity IAP finished initializing.</summary>
        public bool StoreReady => store != null;

        /// <summary>Localized price for the button, e.g. "US$4.99"; falls back to the list price off-store.</summary>
        public string RemoveAdsPrice
        {
            get
            {
                Product product = store?.products?.WithID(RemoveAdsProduct);
                string price = product?.metadata?.localizedPriceString;
                return string.IsNullOrEmpty(price) ? "US$4.99" : price;
            }
        }

        /// <summary>True when the store receipt already carries the entitlement (covers reinstalls).</summary>
        public bool OwnsRemoveAds
        {
            get
            {
                Product product = store?.products?.WithID(RemoveAdsProduct);
                return product != null && product.hasReceipt;
            }
        }

        public void Initialize()
        {
            AdsRemoved = PlayerPrefs.GetInt(RemovedPref, 0) == 1;
            InitializeStore();
#if UNITY_ANDROID && !UNITY_EDITOR
            if (initializing || AdsRemoved)
                return;
            initializing = true;
            MobileAds.Initialize(_ =>
            {
                LoadInterstitial();
                LoadRewarded();
            });
#endif
        }

        /// <summary>Wire Unity IAP regardless of platform so the entitlement is known even before AdMob wakes.</summary>
        void InitializeStore()
        {
            if (store != null)
                return;
            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            builder.AddProduct(RemoveAdsProduct, ProductType.NonConsumable);
            UnityPurchasing.Initialize(this, builder);
        }

        /// <summary>About sheet's "Remove Ads" button.</summary>
        public void BuyRemoveAds()
        {
            if (store == null)
                return;
            store.InitiatePurchase(RemoveAdsProduct);
        }

        /// <summary>About sheet's restore line — pulls the receipt back after a reinstall.</summary>
        public void RestorePurchases()
        {
            if (extensions == null)
                return;
#if UNITY_ANDROID && !UNITY_EDITOR
            extensions.GetExtension<IGooglePlayStoreExtensions>()
                .RestoreTransactions(_ => { });
#else
            // Apple/App Store style API on other platforms; Android restores through Google Play above.
            extensions.GetExtension<UnityEngine.Purchasing.IAppleExtensions>()
                ?.RestoreTransactions(_ => { });
#endif
        }

        // ---- IDetailedStoreListener -------------------------------------------------

        public void OnInitialized(IStoreController controller, IExtensionProvider provider)
        {
            store = controller;
            extensions = provider;
            if (OwnsRemoveAds)
                SetAdsRemoved();
        }

        public void OnInitializeFailed(InitializationFailureReason error)
        {
            Debug.LogWarning($"Oathfire store offline: {error}");
        }

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            Debug.LogWarning($"Oathfire store offline: {error} — {message}");
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            if (args.purchasedProduct.definition.id == RemoveAdsProduct)
                SetAdsRemoved();
            return PurchaseProcessingResult.Complete;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
        {
            Debug.Log($"Oathfire purchase declined: {failureDescription?.reason}");
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            Debug.Log($"Oathfire purchase declined: {failureReason}");
        }

        void SetAdsRemoved()
        {
            if (AdsRemoved)
                return;
            AdsRemoved = true;
            PlayerPrefs.SetInt(RemovedPref, 1);
            PlayerPrefs.Save();
#if UNITY_ANDROID && !UNITY_EDITOR
            banner?.Destroy();
            banner = null;
            interstitial = null;
#endif
            AdsRemovedChanged?.Invoke();
        }

        // ---- Placements ------------------------------------------------------------

        /// <summary>Title screen only: an adaptive banner pinned to the bottom safe edge.</summary>
        public void EnsureBanner()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (AdsRemoved)
                return;
            if (banner != null)
            {
                banner.Show();
                return;
            }
            AdSize size = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth);
            banner = new BannerView(BannerUnit, size, AdPosition.Bottom);
            banner.LoadAd(new AdRequest());
            banner.Show();
#endif
        }

        public void HideBanner()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            banner?.Hide();
#endif
        }

        /// <summary>Show the interstitial at a session boundary. All callers share one global cooldown and a
        /// session cap, so stacking call sites can never make ads feel frequent.</summary>
        public void ShowInterstitialIfReady()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (AdsRemoved)
                return;
            if (interstitial == null || interstitialsShown >= MaxInterstitialsPerSession
                || Time.unscaledTime - lastInterstitialTime < InterstitialCooldownMinutes * 60f)
            {
                LoadInterstitial();
                return;
            }
            InterstitialAd ad = interstitial;
            interstitial = null;
            interstitialsShown++;
            lastInterstitialTime = Time.unscaledTime;
            ad.OnAdFullScreenContentClosed += LoadInterstitial;
            ad.OnAdFullScreenContentFailed += _ => LoadInterstitial();
            ad.Show();
#endif
        }

        /// <summary>Equipment and journal pages call this on close; only every Nth open can show an ad,
        /// and the global cooldown still applies — the book never becomes a billboard.</summary>
        public void NoteMenuClosed()
        {
            if (AdsRemoved)
                return;
            menuOpens++;
            if (menuOpens < MenuOpensPerAd)
                return;
            menuOpens = 0;
            ShowInterstitialIfReady();
        }

        /// <summary>Rewarded opt-in. Fails closed (false) when no ad is loaded rather than blocking play.</summary>
        public void ShowRewarded(Action<bool> onDone)
        {
            if (AdsRemoved)
            {
                onDone?.Invoke(false);
                return;
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            if (rewarded == null || !rewarded.CanShowAd())
            {
                LoadRewarded();
                onDone?.Invoke(false);
                return;
            }
            RewardedAd ad = rewarded;
            rewarded = null;
            ad.OnAdFullScreenContentClosed += LoadRewarded;
            ad.OnAdFullScreenContentFailed += _ =>
            {
                LoadRewarded();
                onDone?.Invoke(false);
            };
            ad.Show(_ => onDone?.Invoke(true));
#else
            onDone?.Invoke(true);
#endif
        }

        /// <summary>Benefactor gift guard: true when the cool-off has lapsed.</summary>
        public bool GiftReady => !AdsRemoved && Time.unscaledTime - lastGiftTime > GiftCooldownMinutes * 60f;

        /// <summary>True when a rewarded ad is loaded and can be shown.</summary>
        public bool RewardedReady =>
            !AdsRemoved &&
#if UNITY_ANDROID && !UNITY_EDITOR
            rewarded != null && rewarded.CanShowAd();
#else
            true;
#endif

        public void NoteGiftTaken() => lastGiftTime = Time.unscaledTime;

#if UNITY_ANDROID && !UNITY_EDITOR
        void LoadInterstitial()
        {
            if (AdsRemoved)
                return;
            InterstitialAd.Load(InterstitialUnit, new AdRequest(), (ad, error) => interstitial = ad);
        }

        void LoadRewarded()
        {
            if (AdsRemoved)
                return;
            RewardedAd.Load(RewardedUnit, new AdRequest(), (ad, error) => rewarded = ad);
        }
#endif
    }
}
