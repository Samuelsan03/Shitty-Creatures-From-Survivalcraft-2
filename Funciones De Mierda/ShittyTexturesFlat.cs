using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public abstract class ShittyTexturesFlat : Block
	{
		// Token: 0x0400000C RID: 12
		public Texture2D m_texture;

		// Token: 0x0400000D RID: 13
		public string texture;

		// Token: 0x0600001B RID: 27 RVA: 0x0000233D File Offset: 0x0000053D
		public ShittyTexturesFlat(string textureName)
		{
			this.texture = textureName;

		}
		// Token: 0x0600001C RID: 28 RVA: 0x0000244C File Offset: 0x0000064C
		public override void Initialize()
		{
			base.Initialize();
			if (!string.IsNullOrEmpty(this.texture))
			{
				this.m_texture = ContentManager.Get<Texture2D>(this.texture, null);
			}
		}

		// Token: 0x0600001D RID: 29 RVA: 0x00002466 File Offset: 0x00000666
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			// Los bloques planos 2D no generan geometría de terreno
		}

		// Token: 0x0600001E RID: 30 RVA: 0x00002468 File Offset: 0x00000668
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			if (this.m_texture != null)
			{
				BlocksManager.DrawFlatBlock(primitivesRenderer, value, size * 1f, ref matrix, this.m_texture, Color.White, true, environmentData);
			}
		}

		// Token: 0x0600001F RID: 31 RVA: 0x000024A0 File Offset: 0x000006A0
		public override BlockPlacementData GetPlacementValue(SubsystemTerrain subsystemTerrain, ComponentMiner componentMiner, int value, TerrainRaycastResult raycastResult)
		{
			// Crear datos de colocación con la cara de la celda
			BlockPlacementData placementData = new BlockPlacementData();
			placementData.Value = value;
			placementData.CellFace = raycastResult.CellFace;
			return placementData;
		}

		// Token: 0x06000020 RID: 32 RVA: 0x000024CC File Offset: 0x000006CC
		public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
		{
			// Sin cajas de colisión para bloques planos
			return new BoundingBox[0];
		}

		// Token: 0x06000021 RID: 33 RVA: 0x000024D4 File Offset: 0x000006D4
		public override BoundingBox[] GetCustomInteractionBoxes(SubsystemTerrain terrain, int value)
		{
			// Sin cajas de interacción para bloques planos
			return new BoundingBox[0];
		}

		// Token: 0x06000022 RID: 34 RVA: 0x000024DC File Offset: 0x000006DC
		public override bool IsFaceTransparent(SubsystemTerrain subsystemTerrain, int face, int value)
		{
			// Todas las caras son transparentes
			return true;
		}

		// Token: 0x06000023 RID: 35 RVA: 0x000024E0 File Offset: 0x000006E0
		public override bool ShouldGenerateFace(SubsystemTerrain subsystemTerrain, int face, int value, int neighborValue, int x, int y, int z)
		{
			// No generar caras para este bloque
			return false;
		}

		// Token: 0x06000024 RID: 36 RVA: 0x000024E4 File Offset: 0x000006E4
		public override int GetFaceTextureSlot(int face, int value)
		{
			// Todas las caras usan la misma textura
			return 0;
		}

		// Token: 0x06000025 RID: 37 RVA: 0x000024E8 File Offset: 0x000006E8
		public override void GetDropValues(SubsystemTerrain subsystemTerrain, int oldValue, int newValue, int toolLevel, List<BlockDropValue> dropValues, out bool showDebris)
		{
			// Sin gotas al destruir
			showDebris = false;
		}

		// Token: 0x06000028 RID: 40 RVA: 0x000024FC File Offset: 0x000006FC
		public override int GetTextureSlotCount(int value)
		{
			// Solo una textura
			return 1;
		}
	}
}
