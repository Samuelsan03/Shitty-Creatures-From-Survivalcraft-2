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
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawFlatBlock(primitivesRenderer, value, size * 0.6f, ref matrix, this.m_texture, Color.White, true, environmentData);
		}

		public override BlockDebrisParticleSystem CreateDebrisParticleSystem(SubsystemTerrain subsystemTerrain, Vector3 position, int value, float strength)
		{
			return new BlockDebrisParticleSystem(subsystemTerrain, position, strength, 0.1f, Color.White, this.GetFaceTextureSlot(4, value), this.m_texture);
		}

		public override IEnumerable<int> GetCreativeValues()
		{
			yield return Terrain.MakeBlockValue(this.BlockIndex, 0, 0);
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
			return FlameBulletBlock.TextureSlot;
		}

		public static FlameBulletBlock.FlameBulletType GetBulletType(int data)
		{
			return (FlameBulletBlock.FlameBulletType)(data & 15);
		}

		public static int SetBulletType(int data, FlameBulletBlock.FlameBulletType flameBulletType)
		{
			return (data & -16) | (int)(flameBulletType & (FlameBulletBlock.FlameBulletType)15);
		}

		public static int Index = 737;
		public Texture2D m_texture;
		public static int TextureSlot = 68;

		public enum FlameBulletType
		{
			Flame = 0
		}
	}

}