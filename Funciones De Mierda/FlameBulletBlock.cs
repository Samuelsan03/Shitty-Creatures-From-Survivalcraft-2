using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Engine.Media;
using Game;

namespace Game
{
	public class FlameBulletBlock : Block
	{
		public override void Initialize()
		{
			base.Initialize();
			this.m_texture = ContentManager.Get<Texture2D>("Textures/WonderfulEra", null);
			this.m_texturePoison = ContentManager.Get<Texture2D>("Textures/WonderfulEra", null);
			this.m_blockMeshes = new BlockMesh[2];
			this.m_blockMeshes[0] = this.CreateBulletMesh(this.m_texture, FlameBulletBlock.TextureSlot);
			this.m_blockMeshes[1] = this.CreateBulletMesh(this.m_texturePoison, FlameBulletBlock.PoisonTextureSlot);
		}

		private BlockMesh CreateBulletMesh(Texture2D texture, int textureSlot)
		{
			var blockMesh = new BlockMesh();
			return blockMesh;
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			int bulletType = (int)FlameBulletBlock.GetBulletType(Terrain.ExtractData(value));
			Texture2D texture = (bulletType == 0) ? this.m_texture : this.m_texturePoison;
			BlocksManager.DrawFlatBlock(primitivesRenderer, value, size * 0.6f, ref matrix, texture, Color.White, true, environmentData);
		}

		public override BlockDebrisParticleSystem CreateDebrisParticleSystem(SubsystemTerrain subsystemTerrain, Vector3 position, int value, float strength)
		{
			int bulletType = (int)FlameBulletBlock.GetBulletType(Terrain.ExtractData(value));
			Texture2D texture = (bulletType == 0) ? this.m_texture : this.m_texturePoison;
			int textureSlot = (bulletType == 0) ? TextureSlot : PoisonTextureSlot;
			return new BlockDebrisParticleSystem(subsystemTerrain, position, strength, 0.1f, Color.White, textureSlot, texture);
		}

		public override IEnumerable<int> GetCreativeValues()
		{
			yield return Terrain.MakeBlockValue(this.BlockIndex, 0, FlameBulletBlock.SetBulletType(0, FlameBulletBlock.FlameBulletType.Flame));
			yield return Terrain.MakeBlockValue(this.BlockIndex, 0, FlameBulletBlock.SetBulletType(0, FlameBulletBlock.FlameBulletType.Poison));
		}

		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			return LanguageControl.GetBlock("FlameBullet:" + (int)FlameBulletBlock.GetBulletType(Terrain.ExtractData(value)), "DisplayName");
		}

		public override string GetDescription(int value)
		{
			return LanguageControl.GetBlock("FlameBullet:" + (int)FlameBulletBlock.GetBulletType(Terrain.ExtractData(value)), "Description");
		}

		public override float GetProjectilePower(int value)
		{
			return 0f;
		}

		public override float GetExplosionPressure(int value)
		{
			return 0f;
		}

		public override int GetFaceTextureSlot(int face, int value)
		{
			if (FlameBulletBlock.GetBulletType(Terrain.ExtractData(value)) != FlameBulletBlock.FlameBulletType.Poison)
			{
				return FlameBulletBlock.TextureSlot;
			}
			return FlameBulletBlock.PoisonTextureSlot;
		}

		public static FlameBulletBlock.FlameBulletType GetBulletType(int data)
		{
			return (FlameBulletBlock.FlameBulletType)(data & 15);
		}

		public static int SetBulletType(int data, FlameBulletBlock.FlameBulletType flameBulletType)
		{
			return (data & -16) | (int)(flameBulletType & (FlameBulletBlock.FlameBulletType)15);
		}

		public static int Index = 320;
		public Texture2D m_texture;
		public Texture2D m_texturePoison;
		public BlockMesh[] m_blockMeshes;
		public static int TextureSlot = 0;
		public static int PoisonTextureSlot = 69;

		public enum FlameBulletType
		{
			Flame = 0,
			Poison = 1
		}
	}
}