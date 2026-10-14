using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;

namespace Game
{
	public abstract class BaseWatermelonBlock : Block
	{
		public BlockMesh[] m_blockMeshesBySize = new BlockMesh[8];
		public BlockMesh[] m_standaloneBlockMeshesBySize = new BlockMesh[8];
		public BoundingBox[][] m_collisionBoxesBySize = new BoundingBox[8][];
		public bool m_isRotten;

		public Texture2D m_ripeTexture;
		public Texture2D m_rottenTexture;

		// Set this to the RottenWatermelonBlock index after registration
		public static int RottenBlockIndex = 243;

		public BaseWatermelonBlock(bool isRotten)
		{
			this.m_isRotten = isRotten;
		}

		public override void Initialize()
		{
			// Load the two textures
			this.m_ripeTexture = ContentManager.Get<Texture2D>("Textures/alimentos/textura melon sandia");
			this.m_rottenTexture = ContentManager.Get<Texture2D>("Textures/alimentos/textura melon sandia podrida");

			// Load watermelon model
			Model model = ContentManager.Get<Model>("Models/melon de sandia");
			ModelMesh modelMesh = model.Meshes[0];
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(modelMesh.ParentBone);

			for (int i = 0; i < 8; i++)
			{
				float num = MathUtils.Lerp(0.2f, 1f, (float)i / 7f);
				float num2 = MathF.Min(0.3f * num, 0.7f * (1f - num));

				Color color;
				if (this.m_isRotten)
				{
					// Color for rotten (user requested "color para la podrida")
					color = new Color(120, 100, 70);
				}
				else
				{
					// Green color lerp for growing stages
					color = Color.Lerp(new Color(0, 100, 0), new Color(80, 255, 80), (float)i / 7f);
					if (i == 7)
					{
						color.R = byte.MaxValue;
					}
				}

				// In-world mesh
				this.m_blockMeshesBySize[i] = new BlockMesh();
				if (i >= 1)
				{
					this.m_blockMeshesBySize[i].AppendModelMeshPart(
						modelMesh.MeshParts[0],
						boneAbsoluteTransform * Matrix.CreateScale(num) * Matrix.CreateTranslation(0.5f + num2, 0f, 0.5f + num2),
						false, false, false, false, color);
				}
				// NOTE: No TransformTextureCoordinates for rotten — we use a separate texture instead

				// Standalone (inventory / hand) mesh
				this.m_standaloneBlockMeshesBySize[i] = new BlockMesh();
				this.m_standaloneBlockMeshesBySize[i].AppendModelMeshPart(
					modelMesh.MeshParts[0],
					boneAbsoluteTransform * Matrix.CreateScale(num) * Matrix.CreateTranslation(0f, -0.23f, 0f),
					false, false, false, false, color);
			}

			// Collision boxes
			for (int j = 0; j < 8; j++)
			{
				BoundingBox boundingBox = (this.m_blockMeshesBySize[j].Vertices.Count > 0)
					? this.m_blockMeshesBySize[j].CalculateBoundingBox()
					: new BoundingBox(new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f));
				float num3 = boundingBox.Max.X - boundingBox.Min.X;
				if (num3 < 0.8f)
				{
					float num4 = (0.8f - num3) / 2f;
					boundingBox.Min.X -= num4;
					boundingBox.Min.Z -= num4;
					boundingBox.Max.X += num4;
					boundingBox.Max.Y = 0.4f;
					boundingBox.Max.Z += num4;
				}
				this.m_collisionBoxesBySize[j] = new BoundingBox[] { boundingBox };
			}

			base.Initialize();
		}

		// ── Texture selection: ripe texture for non-rotten, rotten texture for rotten ──
		public override Texture2D GetDefaultTexture(int value)
		{
			return this.m_isRotten ? this.m_rottenTexture : this.m_ripeTexture;
		}

		public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
		{
			int size = BaseWatermelonBlock.GetSize(Terrain.ExtractData(value));
			return this.m_collisionBoxesBySize[size];
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			Texture2D defaultTexture = this.GetDefaultTexture(value);
			TerrainGeometrySubset subset = (defaultTexture == null)
				? geometry.SubsetOpaque
				: geometry.GetGeometry(defaultTexture).SubsetOpaque;

			int data = Terrain.ExtractData(value);
			int size = BaseWatermelonBlock.GetSize(data);
			bool isDead = BaseWatermelonBlock.GetIsDead(data);

			if (size >= 1)
			{
				generator.GenerateMeshVertices(this, x, y, z, this.m_blockMeshesBySize[size], Color.White, null, subset);
			}

			// Crossface (seedling / leaves) always uses default terrain atlas
			if (size == 0)
			{
				generator.GenerateCrossfaceVertices(this, value, x, y, z, new Color(160, 160, 160), 11, geometry.SubsetAlphaTest);
				return;
			}
			if (size < 7 && !isDead)
			{
				generator.GenerateCrossfaceVertices(this, value, x, y, z, Color.White, 28, geometry.SubsetAlphaTest);
			}
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			int size2 = BaseWatermelonBlock.GetSize(Terrain.ExtractData(value));
			Texture2D defaultTexture = this.GetDefaultTexture(value);
			if (defaultTexture == null)
			{
				BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMeshesBySize[size2], color, 2f * size, ref matrix, environmentData);
				return;
			}
			BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMeshesBySize[size2], defaultTexture, color, 2f * size, ref matrix, environmentData);
		}

		public override int GetShadowStrength(int value)
		{
			return BaseWatermelonBlock.GetSize(Terrain.ExtractData(value));
		}

		public override float GetNutritionalValue(int value)
		{
			if (BaseWatermelonBlock.GetSize(Terrain.ExtractData(value)) != 7)
			{
				return 0f;
			}
			return base.GetNutritionalValue(value);
		}

		// ── Drop logic ──
		// At 100% (size 7, not dead): drop watermelon slices via CSV DefaultDropContent
		// Other stages: drop the block itself (with IsDead = true)
		public override void GetDropValues(SubsystemTerrain subsystemTerrain, int oldValue, int newValue, int toolLevel, List<BlockDropValue> dropValues, out bool showDebris)
		{
			int data = Terrain.ExtractData(oldValue);
			int size = BaseWatermelonBlock.GetSize(data);
			bool isDead = BaseWatermelonBlock.GetIsDead(data);

			if (!this.m_isRotten && size == 7 && !isDead)
			{
				// 100% ripe → drop slices (CSV DefaultDropContent handles this)
				showDebris = (this.DestructionDebrisScale > 0f);
				if (toolLevel < this.RequiredToolLevel)
				{
					return;
				}
				if (this.DefaultDropContent != 0)
				{
					int count = (int)this.DefaultDropCount;
					if (this.Random.Bool(this.DefaultDropCount - (float)count))
					{
						count++;
					}
					for (int i = 0; i < count; i++)
					{
						dropValues.Add(new BlockDropValue
						{
							Value = Terrain.MakeBlockValue(this.DefaultDropContent),
							Count = 1
						});
					}
				}
			}
			else if (size >= 1)
			{
				// Other stages → drop the block itself
				int value = this.SetDamage(
					Terrain.MakeBlockValue(this.BlockIndex, 0,
						BaseWatermelonBlock.SetSize(BaseWatermelonBlock.SetIsDead(0, true), size)),
					this.GetDamage(oldValue));
				dropValues.Add(new BlockDropValue
				{
					Value = value,
					Count = 1
				});
				showDebris = true;
			}
			else
			{
				showDebris = true;
			}
		}

		public override BlockDebrisParticleSystem CreateDebrisParticleSystem(SubsystemTerrain subsystemTerrain, Vector3 position, int value, float strength)
		{
			int size = BaseWatermelonBlock.GetSize(Terrain.ExtractData(value));
			float num = MathUtils.Lerp(0.2f, 1f, (float)size / 7f);

			// Si está podrida usamos la textura podrida, de lo contrario la madura
			Texture2D debrisTexture = this.m_isRotten ? this.m_rottenTexture : this.m_ripeTexture;

			// Color blanco para que no afecte el tinte de la textura
			Color color = Color.White;

			// Usamos el constructor que permite pasar una Texture2D personalizada
			// y pasamos 0 como textureSlot para que lea la textura completa.
			return new BlockDebrisParticleSystem(subsystemTerrain, position, 1.5f * strength, this.DestructionDebrisScale * num, color, 0, debrisTexture);
		}

		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			string text = "BaseWatermelonBlock";
			int size = BaseWatermelonBlock.GetSize(Terrain.ExtractData(value));
			if (this.m_isRotten)
			{
				if (size >= 7)
				{
					return string.Format(LanguageControl.Get(new string[] { text, "2" }),
						LanguageControl.Get(new string[] { text, "4" }));
				}
				return string.Format(LanguageControl.Get(new string[] { text, "3" }),
					LanguageControl.Get(new string[] { text, "4" }),
					LanguageControl.Get(new string[] { text, "5" }));
			}
			else
			{
				if (size >= 7)
				{
					return LanguageControl.Get(new string[] { text, "1" });
				}
				return string.Format(LanguageControl.Get(new string[] { text, "2" }),
					LanguageControl.Get(new string[] { text, "5" }));
			}
		}

		// ── Creative: all 8 growth stages ──
		public override IEnumerable<int> GetCreativeValues()
		{
			for (int size = 0; size < 8; size++)
			{
				int data = BaseWatermelonBlock.SetSize(0, size);
				yield return Terrain.MakeBlockValue(this.BlockIndex, 0, data);
			}
		}

		// ── Data bit manipulation (identical to pumpkin) ──
		public static int GetSize(int data)
		{
			return 7 - (data & 7);
		}

		public static int SetSize(int data, int size)
		{
			return (data & -8) | (7 - (size & 7));
		}

		public static bool GetIsDead(int data)
		{
			return (data & 8) != 0;
		}

		public static int SetIsDead(int data, bool isDead)
		{
			return isDead ? (data | 8) : (data & -9);
		}

		public override int GetDamage(int value)
		{
			return (Terrain.ExtractData(value) & 16) >> 4;
		}

		public override int SetDamage(int value, int damage)
		{
			int num = Terrain.ExtractData(value);
			return Terrain.ReplaceData(value, (num & -17) | ((damage & 1) << 4));
		}

		public override int GetDamageDestructionValue(int value)
		{
			if (this.m_isRotten)
			{
				return 0;
			}
			int data = Terrain.ExtractData(value);
			return this.SetDamage(Terrain.MakeBlockValue(BaseWatermelonBlock.RottenBlockIndex, 0, data), 0);
		}

		public override int GetRotPeriod(int value)
		{
			if (!BaseWatermelonBlock.GetIsDead(Terrain.ExtractData(value)))
			{
				return 0;
			}
			return this.DefaultRotPeriod;
		}

		public override bool IsSuitableForPlants(int value, int plantValue)
		{
			return false;
		}
	}
}
