using System;
using System.Collections;
using UnityEngine;

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
    ///   interstitial — session boundaries: pause-menu Return to Title, and the ending cinematic handing back
    ///                  to Title. Never mid-fight, never mid-quest.
    ///   rewarded     — opt-in only: keep the fallen Warden's coin (defeat screen) and the benefactor's
    ///                  coin gift in the pause menu (once per cool-off).
    /// </summary>
    public class AdsService : MonoBehaviour
    {
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
        bool initializing;
        float lastGiftTime = float.MinValue;
        float lastInterstitialTime = float.MinValue;
        int interstitialsShown;
        int menuOpens;

        public void Initialize()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (initializing)
                return;
            initializing = true;
            MobileAds.Initialize(_ =>
            {
                LoadInterstitial();
                LoadRewarded();
            });
#endif
        }

        /// <summary>Title screen only: an adaptive banner pinned to the bottom safe edge.</summary>
        public void EnsureBanner()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
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
            menuOpens++;
            if (menuOpens < MenuOpensPerAd)
                return;
            menuOpens = 0;
            ShowInterstitialIfReady();
        }

        /// <summary>Rewarded opt-in. Fails closed (false) when no ad is loaded rather than blocking play.</summary>
        public void ShowRewarded(Action<bool> onDone)
        {
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
        public bool GiftReady => Time.unscaledTime - lastGiftTime > GiftCooldownMinutes * 60f;

        /// <summary>True when a rewarded ad is loaded and can be shown.</summary>
        public bool RewardedReady =>
#if UNITY_ANDROID && !UNITY_EDITOR
            rewarded != null && rewarded.CanShowAd();
#else
            true;
#endif

        public void NoteGiftTaken() => lastGiftTime = Time.unscaledTime;

#if UNITY_ANDROID && !UNITY_EDITOR
        void LoadInterstitial()
        {
            InterstitialAd.Load(InterstitialUnit, new AdRequest(), (ad, error) => interstitial = ad);
        }

        void LoadRewarded()
        {
            RewardedAd.Load(RewardedUnit, new AdRequest(), (ad, error) => rewarded = ad);
        }
#endif
    }
}
