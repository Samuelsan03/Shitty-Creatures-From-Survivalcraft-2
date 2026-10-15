using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class SlingshotBlock : Block
	{
		public enum AmmoType
		{
			Empty,
			Stone,
			Snowball,
			BuckshotBall
		}

		public Block m_stoneChunkBlock;
		public Block m_snowballBlock;
		public Block m_bulletBlock;

		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Slingshot");

			Matrix boneBody = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Body", true).ParentBone);
			Matrix boneRelaxed = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("StringRelaxed", true).ParentBone);
			Matrix boneTensed = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("StringTensed", true).ParentBone);

			BlockMesh relaxedMesh = new BlockMesh();
			relaxedMesh.AppendModelMeshPart(model.FindMesh("Body", true).MeshParts[0], boneBody * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false, Color.White);
			relaxedMesh.AppendModelMeshPart(model.FindMesh("StringRelaxed", true).MeshParts[0], boneRelaxed * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false, Color.White);

			BlockMesh tensedMesh = new BlockMesh();
			tensedMesh.AppendModelMeshPart(model.FindMesh("Body", true).MeshParts[0], boneBody * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false, Color.White);
			tensedMesh.AppendModelMeshPart(model.FindMesh("StringTensed", true).MeshParts[0], boneTensed * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false, Color.White);

			for (int i = 0; i < 16; i++)
			{
				float factor = (float)i / 15f;
				this.m_standaloneBlockMeshes[i] = new BlockMesh();
				this.m_standaloneBlockMeshes[i].AppendBlockMesh(relaxedMesh);
				this.m_standaloneBlockMeshes[i].BlendBlockMesh(tensedMesh, factor);
			}

			// Cargar los bloques de munición para poder dibujarlos visualmente al tensar
			this.m_stoneChunkBlock = BlocksManager.GetBlockGeneral<StoneChunkBlock>(false);
			this.m_snowballBlock = BlocksManager.GetBlockGeneral<SnowballBlock>(false);
			this.m_bulletBlock = BlocksManager.GetBlockGeneral<BulletBlock>(false);

			this.IsEditable = true;
			this.IsAimable = true;

			base.Initialize();
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			int data = Terrain.ExtractData(value);
			int draw = SlingshotBlock.GetDraw(data);
			AmmoType ammoType = SlingshotBlock.GetAmmoType(data);

			Texture2D defaultTexture = this.GetDefaultTexture(value);
			if (defaultTexture == null)
			{
				BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMeshes[draw], color, 2f * size, ref matrix, environmentData);
			}
			else
			{
				BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMeshes[draw], defaultTexture, color, 2f * size, ref matrix, environmentData);
			}

			// Dibujar el bloque de munición si hay alguno seleccionado
			if (ammoType != AmmoType.Empty)
			{
				Block block = null;
				int ammoValue = 0;
				switch (ammoType)
				{
					case AmmoType.Stone:
						block = this.m_stoneChunkBlock;
						ammoValue = Terrain.MakeBlockValue(StoneChunkBlock.Index);
						break;
					case AmmoType.Snowball:
						block = this.m_snowballBlock;
						ammoValue = Terrain.MakeBlockValue(SnowballBlock.Index);
						break;
					case AmmoType.BuckshotBall:
						block = this.m_bulletBlock;
						ammoValue = Terrain.MakeBlockValue(BulletBlock.Index, 0, BulletBlock.SetBulletType(0, BulletBlock.BulletType.BuckshotBall));
						break;
				}

				if (block != null)
				{
					// Fisicas de resortera: Se coloca en la horqueta y se hala directo hacia atrás (Z positivo).
					// Va desde -0.4f (descansando al frente) hasta 1.2f (totalmente tensado hacia el jugador).
					float pull = MathUtils.Lerp(-0.4f, 0.85f, (float)draw / 15f);
					Matrix matrix2 = Matrix.CreateRotationX(-1.5707964f) * Matrix.CreateTranslation(0f, 0.35f * size, pull * size) * matrix;

					// Reducir el tamaño de la munición (50% del tamaño original)
					float ammoSize = 0.5f * size;
					block.DrawBlock(primitivesRenderer, ammoValue, color, ammoSize, ref matrix2, environmentData);
				}
			}
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

		public override bool IsSwapAnimationNeeded(int oldValue, int newValue)
		{
			if (Terrain.ExtractContents(oldValue) == this.BlockIndex)
			{
				int data = Terrain.ExtractData(oldValue);
				int data2 = Terrain.ExtractData(newValue);
				if (SlingshotBlock.GetAmmoType(data) == SlingshotBlock.GetAmmoType(data2))
				{
					return false;
				}
			}
			return true;
		}

		public static int GetDraw(int data)
		{
			return data & 15;
		}

		public static int SetDraw(int data, int draw)
		{
			return (data & -16) | (draw & 15);
		}

		public static SlingshotBlock.AmmoType GetAmmoType(int data)
		{
			return (SlingshotBlock.AmmoType)((data >> 4) & 15);
		}

		public static int SetAmmoType(int data, SlingshotBlock.AmmoType ammoType)
		{
			return (data & -241) | ((int)ammoType & 15) << 4;
		}

		public static int Index = 682;

		public BlockMesh[] m_standaloneBlockMeshes = new BlockMesh[16];
	}
}
