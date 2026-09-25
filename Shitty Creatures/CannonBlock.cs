using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class CannonBlock : Block
	{
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/ItemsLauncher");
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Cylinder", true).ParentBone);
			this.m_standaloneBlockMesh = new BlockMesh();
			this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Cylinder", true).MeshParts[0], boneAbsoluteTransform, false, false, false, false, Color.White);
			base.Initialize();
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			// Escala ajustada a 1.5f
			Texture2D defaultTexture = this.GetDefaultTexture(value);
			if (defaultTexture == null)
			{
				BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, color, 1.5f * size, ref matrix, environmentData);
				return;
			}
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, defaultTexture, color, 1.5f * size, ref matrix, environmentData);
		}

		public override bool IsSwapAnimationNeeded(int oldValue, int newValue)
		{
			if (Terrain.ExtractContents(oldValue) != this.BlockIndex)
			{
				return true;
			}

			int data = Terrain.ExtractData(oldValue);
			return CannonBlock.SetHammerState(Terrain.ExtractData(newValue), true) !=
				   CannonBlock.SetHammerState(data, true);
		}

		public override int GetDamage(int value)
		{
			return Terrain.ExtractData(value) >> 8 & 255;
		}

		public override int SetDamage(int value, int damage)
		{
			int num = Terrain.ExtractData(value);
			num &= -65281;
			num |= Math.Clamp(damage, 0, 255) << 8;
			return Terrain.ReplaceData(value, num);
		}

		public static CannonBlock.LoadState GetLoadState(int data)
		{
			return (CannonBlock.LoadState)(data & 3);
		}

		public static int SetLoadState(int data, CannonBlock.LoadState loadState)
		{
			return (data & -4) | (int)(loadState & CannonBlock.LoadState.Loaded);
		}

		public static bool GetHammerState(int data)
		{
			return (data & 4) != 0;
		}

		public static int SetHammerState(int data, bool state)
		{
			return (data & -5) | ((state ? 1 : 0) << 2);
		}

		public static CannonBallBlock.CannonBallType? GetCannonBallType(int data)
		{
			int num = data >> 4 & 15;
			if (num != 0)
			{
				return new CannonBallBlock.CannonBallType?((CannonBallBlock.CannonBallType)(num - 1));
			}
			return null;
		}

		public static int SetCannonBallType(int data, CannonBallBlock.CannonBallType? cannonBallType)
		{
			int num = (int)((cannonBallType != null) ? (cannonBallType.Value + 1) : CannonBallBlock.CannonBallType.CannonBall);
			return (data & -241) | (num & 15) << 4;
		}

		public static int Index = 554;

		public BlockMesh m_standaloneBlockMesh;

		public enum LoadState
		{
			Empty,
			Loaded
		}
	}
}
