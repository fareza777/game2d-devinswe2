using TMPro;
using UnityEngine;

namespace Oathfire.UI
{
    /// <summary>
    /// One place for the game's look: the two typefaces and the ink/bone/ember palette every screen uses.
    /// Body text inherits EB Garamond from TMP's default; headings ask for Cinzel through here.
    /// </summary>
    public static class UiTheme
    {
        public static readonly Color Ink = new Color(0.055f, 0.063f, 0.051f);
        public static readonly Color InkPanel = new Color(0.07f, 0.08f, 0.065f, 0.96f);
        public static readonly Color Bone = new Color(0.87f, 0.84f, 0.76f);
        public static readonly Color BoneDim = new Color(0.66f, 0.64f, 0.58f);
        public static readonly Color Ember = new Color(0.79f, 0.58f, 0.23f);
        public static readonly Color EmberBright = new Color(1f, 0.72f, 0.3f);
        public static readonly Color Moss = new Color(0.37f, 0.54f, 0.51f);
        public static readonly Color Rust = new Color(0.69f, 0.34f, 0.25f);

        static TMP_FontAsset display;
        static TMP_FontAsset body;

        public static TMP_FontAsset Display => display ??= Load("Cinzel-Bold");
        public static TMP_FontAsset Body => body ??= Load("EBGaramond-Regular");

        static TMP_FontAsset Load(string name)
        {
            var font = Resources.Load<TMP_FontAsset>($"Fonts/{name}");
            if (!font)
                Debug.LogWarning($"[UiTheme] Missing font asset Resources/Fonts/{name}; falling back to the TMP default");
            return font;
        }

        /// <summary>Carved-capitals styling for titles, headings and speaker names.</summary>
        public static TMP_Text AsHeading(this TMP_Text text, float letterSpacing = 6f)
        {
            Apply(text, Display);
            text.characterSpacing = letterSpacing;
            text.fontStyle = FontStyles.Normal;
            return text;
        }

        /// <summary>
        /// Writes the atlas the shipped game is actually sampling to a PNG. The asset on disk can be correct
        /// while the build carries a resized or re-encoded copy, and that difference is only visible here.
        /// </summary>
        static void DumpAtlas(TMP_FontAsset font, Texture atlas)
        {
            if (atlas == null)
                return;
            RenderTexture target = RenderTexture.GetTemporary(atlas.width, atlas.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(atlas, target);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;

            var readable = new Texture2D(atlas.width, atlas.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, atlas.width, atlas.height), 0, 0);
            readable.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);

            string path = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(Application.dataPath) ?? ".", $"atlas_{font.name}.png");
            System.IO.File.WriteAllBytes(path, readable.EncodeToPNG());
            Object.Destroy(readable);
            Debug.Log($"[UiTheme] wrote {path}");
        }

        /// <summary>Warm book face for dialogue, descriptions and prompts.</summary>
        public static TMP_Text AsBody(this TMP_Text text)
        {
            Apply(text, Body);
            return text;
        }

        /// <summary>
        /// Swaps the typeface and its atlas together. Setting only <c>font</c> leaves the label on whatever
        /// material it was created with, so the new face's glyph rectangles get sampled out of the old face's
        /// atlas — which renders real letters that spell nothing.
        /// </summary>
        static void Apply(TMP_Text text, TMP_FontAsset font)
        {
            if (!font)
                return;
            text.font = font;
            text.fontSharedMaterial = font.material;
            ReportOnce(font, text);
        }

        static readonly System.Collections.Generic.HashSet<string> reported = new System.Collections.Generic.HashSet<string>();

        /// <summary>
        /// Says once per typeface what the label actually ended up using. Garbled headings have twice been a
        /// mismatch between a font's glyph table and the atlas it samples, which is invisible without this.
        /// </summary>
        static void ReportOnce(TMP_FontAsset font, TMP_Text text)
        {
            if (!Debug.isDebugBuild || !reported.Add(font.name))
                return;
            Texture atlas = text.fontSharedMaterial ? text.fontSharedMaterial.mainTexture : null;
            Debug.Log($"[UiTheme] {font.name}: chars={font.characterTable.Count} atlases={font.atlasTextures?.Length} " +
                      $"mode={font.atlasPopulationMode} material={text.fontSharedMaterial?.name} " +
                      $"sampling={atlas?.name} {atlas?.width}x{atlas?.height}");

            // What the engine resolves for a known string, so a stale or mismatched glyph table shows up as
            // different numbers from the ones sitting in the asset file.
            var probe = new System.Text.StringBuilder($"[UiTheme] {font.name} probe:");
            foreach (char letter in "THEfire")
            {
                if (font.characterLookupTable.TryGetValue(letter, out TMP_Character character) && character.glyph != null)
                {
                    UnityEngine.TextCore.GlyphRect rect = character.glyph.glyphRect;
                    probe.Append($" {letter}=g{character.glyphIndex}({rect.x},{rect.y},{rect.width}x{rect.height})");
                }
                else
                {
                    probe.Append($" {letter}=MISSING");
                }
            }
            Debug.Log(probe.ToString());
            DumpAtlas(font, atlas);
        }
    }
}
