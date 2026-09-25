using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class CannonBallBlock : FlatBlock
	{
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			int cannonBallType = (int)CannonBallBlock.GetCannonBallType(Terrain.ExtractData(value));
			float size2 = (cannonBallType >= 0 && cannonBallType < CannonBallBlock.m_sizes.Length) ? (size * CannonBallBlock.m_sizes[cannonBallType]) : size;
			// Dark iron color for the cannonball
			Color darkColor = Color.MultiplyColorOnly(color, 0.35f);
			BlocksManager.DrawFlatOrImageExtrusionBlock(primitivesRenderer, value, size2, ref matrix, this.GetDefaultTexture(value), darkColor, false, environmentData);
		}

		public override float GetProjectilePower(int value)
		{
			int cannonBallType = (int)CannonBallBlock.GetCannonBallType(Terrain.ExtractData(value));
			if (cannonBallType < 0 || cannonBallType >= CannonBallBlock.m_weaponPowers.Length)
			{
				return 0f;
			}
			return CannonBallBlock.m_weaponPowers[cannonBallType];
		}

		public override float GetExplosionPressure(int value)
		{
			int cannonBallType = (int)CannonBallBlock.GetCannonBallType(Terrain.ExtractData(value));
			if (cannonBallType < 0 || cannonBallType >= CannonBallBlock.m_explosionPressures.Length)
			{
				return 0f;
			}
			return CannonBallBlock.m_explosionPressures[cannonBallType];
		}

		public override IEnumerable<int> GetCreativeValues()
		{
			yield return Terrain.MakeBlockValue(CannonBallBlock.Index, 0, 0);
		}

		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			int cannonBallType = (int)CannonBallBlock.GetCannonBallType(Terrain.ExtractData(value));
			if (cannonBallType < 0 || cannonBallType >= Enum.GetValues<CannonBallBlock.CannonBallType>().Length)
			{
				return string.Empty;
			}
			return LanguageControl.Get("CannonBallBlock", cannonBallType);
		}

		public override int GetFaceTextureSlot(int face, int value)
		{
			int cannonBallType = (int)CannonBallBlock.GetCannonBallType(Terrain.ExtractData(value));
			if (cannonBallType < 0 || cannonBallType >= CannonBallBlock.m_textureSlots.Length)
			{
				return 229;
			}
			return CannonBallBlock.m_textureSlots[cannonBallType];
		}

		public static CannonBallBlock.CannonBallType GetCannonBallType(int data)
		{
			return (CannonBallBlock.CannonBallType)(data & 15);
		}

		public static int SetCannonBallType(int data, CannonBallBlock.CannonBallType cannonBallType)
		{
			return (data & -16) | (int)(cannonBallType & (CannonBallBlock.CannonBallType)15);
		}

		public static int Index = 680;

		// Cannonball is much bigger than musket ball (2f vs 1f)
		public static float[] m_sizes = new float[]
		{
			2f
		};

		public static int[] m_textureSlots = new int[]
		{
			229
		};

		// Much more powerful than musket ball (200f vs 80f)
		public static float[] m_weaponPowers = new float[]
		{
			200f
		};

		public static float[] m_explosionPressures = new float[1];

		public enum CannonBallType
		{
			CannonBall
		}
	}
}
