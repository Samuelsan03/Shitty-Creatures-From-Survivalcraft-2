using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
    /// <summary>
    /// Hereda de ComponentClothing y sube la resolución de TODAS las prendas
    /// (vanilla, de mods o creadas), incluidas las de alta resolución.
    ///
    /// targetSize = max( skin , todasLasPrendas ) * QualityMultiplier
    ///   512x512  con factor 2 => 1024x1024
    ///   1024x1024 con factor 2 => 2048x2048
    ///   Una prenda 2048 con skin 512 => 2048 (o más) * factor; nunca se downscalea.
    /// </summary>
    public class ComponentClothingHD : ComponentClothing
    {
        /// <summary>Multiplicador de resolución. 1 = sin cambios, 2 = x2, 3 = x3...</summary>
        public float QualityMultiplier = 2f;

        /// <summary>true = filtrado lineal (suave). false = PointClamp (pixel-art).</summary>
        public bool UseLinearFiltering = false;

        public override void UpdateRenderTargets()
        {
            if (!ComponentClothing.ShowClothedTexture ||
                this.m_componentPlayer.PlayerData.CharacterSkinName == "$Empty")
                return;

            // (Re)cargar skin si cambió
            if (this.m_skinTexture == null ||
                this.m_componentPlayer.PlayerData.CharacterSkinName != this.m_skinTextureName)
            {
                this.m_skinTexture = CharacterSkinsManager.LoadTexture(
                    this.m_componentPlayer.PlayerData.CharacterSkinName);
                this.m_skinTextureName = this.m_componentPlayer.PlayerData.CharacterSkinName;

                Utilities.Dispose<RenderTarget2D>(ref this.m_innerClothedTexture);
                Utilities.Dispose<RenderTarget2D>(ref this.m_outerClothedTexture);
                this.m_clothedTexturesValid = false;
            }

            // === Resolución base = máximo entre skin y todas las prendas ===
            int baseW = this.m_skinTexture.Width;
            int baseH = this.m_skinTexture.Height;

            foreach (KeyValuePair<ClothingSlot, List<int>> kvp in this.m_clothes)
            {
                foreach (int value in kvp.Value)
                {
                    ClothingData cd = BlocksManager.Blocks[Terrain.ExtractContents(value)]
                                                   .GetClothingData(value);
                    if (cd == null) continue;

                    if (cd.Texture == null && !string.IsNullOrEmpty(cd._textureName))
                        cd.Texture = ContentManager.Get<Texture2D>(cd._textureName);
                    if (cd.Texture == null) continue;

                    if (cd.Texture.Width  > baseW) baseW = cd.Texture.Width;
                    if (cd.Texture.Height > baseH) baseH = cd.Texture.Height;
                }
            }

            int targetW = MathUtils.Max(1, (int)(baseW * QualityMultiplier));
            int targetH = MathUtils.Max(1, (int)(baseH * QualityMultiplier));

            // (Re)crear render targets
            if (this.m_innerClothedTexture == null ||
                this.m_innerClothedTexture.Width  != targetW ||
                this.m_innerClothedTexture.Height != targetH)
            {
                Utilities.Dispose<RenderTarget2D>(ref this.m_innerClothedTexture);
                this.m_innerClothedTexture = new RenderTarget2D(
                    targetW, targetH, 1, ColorFormat.Rgba8888, DepthFormat.None);
                this.m_componentHumanModel.TextureOverride = this.m_innerClothedTexture;
                this.m_clothedTexturesValid = false;
            }
            if (this.m_outerClothedTexture == null ||
                this.m_outerClothedTexture.Width  != targetW ||
                this.m_outerClothedTexture.Height != targetH)
            {
                Utilities.Dispose<RenderTarget2D>(ref this.m_outerClothedTexture);
                this.m_outerClothedTexture = new RenderTarget2D(
                    targetW, targetH, 1, ColorFormat.Rgba8888, DepthFormat.None);
                this.m_componentOuterClothingModel.TextureOverride = this.m_outerClothedTexture;
                this.m_clothedTexturesValid = false;
            }

            if (!ComponentClothing.DrawClothedTexture || this.m_clothedTexturesValid)
                return;

            this.m_clothedTexturesValid = true;

            Rectangle      scissor = Display.ScissorRectangle;
            RenderTarget2D prevRT  = Display.RenderTarget;

            SamplerState sampler    = UseLinearFiltering
                ? SamplerState.LinearClamp
                : SamplerState.PointClamp;
            Vector2      targetSize = new Vector2(targetW, targetH);

            try
            {
                // ---------- INNER ----------
                Display.RenderTarget = this.m_innerClothedTexture;
                Display.Clear(new Vector4?(new Vector4(Color.Transparent)), null, null);

                int layer = 0;

                var skinBatch = this.m_primitivesRenderer.TexturedBatch(
                    this.m_skinTexture, false, layer++, DepthStencilState.None, null,
                    BlendState.NonPremultiplied, sampler);
                skinBatch.QueueQuad(Vector2.Zero, targetSize, 0f,
                                    Vector2.Zero, Vector2.One, Color.White);

                foreach (ClothingSlot slot in ComponentClothing.m_innerSlotsOrder)
                {
                    foreach (int value in this.GetClothes(slot))
                    {
                        ClothingData cd = BlocksManager.Blocks[Terrain.ExtractContents(value)]
                                                       .GetClothingData(value);
                        if (cd == null || cd.IsOuter) continue;

                        if (cd.Texture == null && !string.IsNullOrEmpty(cd._textureName))
                            cd.Texture = ContentManager.Get<Texture2D>(cd._textureName);
                        if (cd.Texture == null) continue;

                        Color color = cd.GetColor(this, value);
                        var batch = this.m_primitivesRenderer.TexturedBatch(
                            cd.Texture, false, layer++, DepthStencilState.None, null,
                            BlendState.NonPremultiplied, sampler);
                        batch.QueueQuad(Vector2.Zero, targetSize, 0f,
                                        Vector2.Zero, Vector2.One, color);
                    }
                }

                this.m_primitivesRenderer.Flush(true, int.MaxValue);

                // ---------- OUTER ----------
                Display.RenderTarget = this.m_outerClothedTexture;
                Display.Clear(new Vector4?(new Vector4(Color.Transparent)), null, null);
                layer = 0;

                foreach (ClothingSlot slot in ComponentClothing.m_outerSlotsOrder)
                {
                    foreach (int value in this.GetClothes(slot))
                    {
                        ClothingData cd = BlocksManager.Blocks[Terrain.ExtractContents(value)]
                                                       .GetClothingData(value);
                        if (cd == null || !cd.IsOuter) continue;

                        if (cd.Texture == null && !string.IsNullOrEmpty(cd._textureName))
                            cd.Texture = ContentManager.Get<Texture2D>(cd._textureName);
                        if (cd.Texture == null) continue;

                        Color color = cd.GetColor(this, value);
                        var batch = this.m_primitivesRenderer.TexturedBatch(
                            cd.Texture, false, layer++, DepthStencilState.None, null,
                            BlendState.NonPremultiplied, sampler);
                        batch.QueueQuad(Vector2.Zero, targetSize, 0f,
                                        Vector2.Zero, Vector2.One, color);
                    }
                }

                this.m_primitivesRenderer.Flush(true, int.MaxValue);
            }
            finally
            {
                Display.RenderTarget  = prevRT;
                Display.ScissorRectangle = scissor;
            }
        }
    }
}
