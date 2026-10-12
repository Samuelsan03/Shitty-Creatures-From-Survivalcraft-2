using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemChristmasGiftEggBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks
		{
			get
			{
				Block eggBlock = BlocksManager.GetBlock("ChristmasGiftEggBlock");
				if (eggBlock != null)
				{
					return new int[] { eggBlock.BlockIndex };
				}
				return Array.Empty<int>();
			}
		}

		public SubsystemGameInfo m_subsystemGameInfo;
		public SubsystemPickables m_subsystemPickables;
		public SubsystemCreatureSpawn m_subsystemCreatureSpawn;
		public ClothingBlock m_clothingBlock;

		public Random m_random = new Random();

		/*
         * Plan de probabilidad (cantidad de objetos que aparecen al arrojar el huevo):
         * 
         * 1 objeto  -> 8%   -> Probabilidad muy baja
         * 2 objetos -> 18%  -> Probabilidad baja
         * 3 objetos -> 28%  -> Probabilidad media-alta
         * 4 objetos -> 24%  -> Probabilidad alta
         * 5 objetos -> 12%  -> Probabilidad media
         * 6 objetos -> 6%   -> Probabilidad media-baja
         * 7 objetos -> 3%   -> Probabilidad baja
         * 8 objetos -> 1%   -> Probabilidad muy baja
         * 
         * Total: 8+18+28+24+12+6+3+1 = 100%
         */
		private static readonly int[] s_giftCounts = { 1, 2, 3, 4, 5, 6, 7, 8 };
		private static readonly float[] s_giftProbabilities = { 0.08f, 0.18f, 0.28f, 0.24f, 0.12f, 0.06f, 0.03f, 0.01f };

		/*
         * Plan de probabilidad para que suelte Órbitas de Experiencia:
         * 
         * 60% -> No suelta experiencia
         * 25% -> Suelta 1 órbita
         * 10% -> Suelta 3 órbitas
         * 5%  -> Suelta 5 órbitas
         */
		private static readonly int[] s_expCounts = { 0, 1, 3, 5 };
		private static readonly float[] s_expProbabilities = { 0.60f, 0.25f, 0.10f, 0.05f };

		// Lista de índices de ropa disponibles basados en el XML de Clothes
		private static readonly int[] s_clothingIndices =
		{
			0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37
		};

		// Lista explícita de bloques según el TXT proporcionado
		private static readonly string[] s_validBlockNames = new string[]
		{
            // Terrain
            "BedrockBlock", "DirtBlock", "SoilBlock", "LimestoneBlock", "GraniteBlock", "BasaltBlock",
			"SandstoneBlock", "GravelBlock", "ClayBlock", "SandBlock", "GrassBlock", "SnowBlock", "IceBlock", "StoneChunkBlock",
            
            // Minerals
            "CoalOreBlock", "CopperOreBlock", "IronOreBlock", "SulphurOreBlock", "DiamondOreBlock", "GermaniumOreBlock",
			"SaltpeterOreBlock", "CoalBlock", "CoalChunkBlock", "MalachiteChunkBlock", "IronOreChunkBlock", "SaltpeterChunkBlock",
			"SulphurChunkBlock", "GermaniumOreChunkBlock", "GermaniumChunkBlock", "DiamondChunkBlock",
            
            // Plants
            "OakWoodBlock", "BirchWoodBlock", "SpruceWoodBlock", "MimosaWoodBlock", "PoplarWoodBlock", "FallenLeavesBlock",
			"OakLeavesBlock", "BirchLeavesBlock", "SpruceLeavesBlock", "TallSpruceLeavesBlock", "MimosaLeavesBlock",
			"PoplarLeavesBlock", "TallGrassBlock", "RyeBlock", "RedFlowerBlock", "PurpleFlowerBlock", "WhiteFlowerBlock",
			"DryBushBlock", "LargeDryBushBlock", "SaplingBlock", "CactusBlock", "IvyBlock", "PumpkinBlock", "RottenPumpkinBlock",
			"SeedsBlock", "CottonBlock", "CottonWadBlock", "SeaUrchinBlock", "StarfishBlock", "KelpBlock", "SeagrassBlock",
            
            // Construction
            "PlanksBlock", "CobblestoneBlock", "StoneBrickBlock", "BricksBlock", "MarbleBlock", "MalachiteBlock", "DiamondBlock",
			"IronBlock", "CopperBlock", "SemiconductorBlock", "GlassBlock", "FramedGlassBlock", "WindowBlock",
			"DirtStairsBlock", "WoodenStairsBlock", "StoneStairsBlock", "CobblestoneStairsBlock", "StoneBrickStairsBlock",
			"BrickStairsBlock", "SandstoneStairsBlock", "MarbleStairsBlock", "BasaltStairsBlock", "DirtSlabBlock",
			"WoodenSlabBlock", "StoneSlabBlock", "CobblestoneSlabBlock", "StoneBrickSlabBlock", "BrickSlabBlock",
			"SandstoneSlabBlock", "MarbleSlabBlock", "BasaltSlabBlock",
            
            // Items
            "BrickBlock", "SnowballBlock", "WoodenDoorBlock", "IronDoorBlock", "CellDoorBlock", "WoodenTrapdoorBlock",
			"CellTrapdoorBlock", "WoodenLadderBlock", "IronLadderBlock", "WickerLampBlock", "TorchBlock", "StickBlock",
			"IronIngotBlock", "CopperIngotBlock", "RodBlock", "CraftingTableBlock", "FurnaceBlock", "LitFurnaceBlock",
			"ChestBlock", "DispenserBlock", "SpikedPlankBlock", "GrassTrapBlock", "WoodenFenceBlock", "WoodenFenceGateBlock",
			"IronFenceBlock", "IronFenceGateBlock", "StoneFenceBlock", "BrickFenceBlock", "BasaltFenceBlock",
			"WoodenPostedSignBlock", "WoodenAttachedSignBlock", "IronPostedSignBlock", "IronAttachedSignBlock",
			"SmallGunpowderKegBlock", "MediumGunpowderKegBlock", "LargeGunpowderKegBlock", "SmallIncendiaryKegBlock",
			"MediumIncendiaryKegBlock", "LargeIncendiaryKegBlock", "GravestoneBlock", "BoatBlock", "FeatherBlock",
			"StringBlock", "CanvasBlock", "CarpetBlock", "CampfireBlock", "GunpowderBlock", "PigmentBlock",
			"JackOLanternBlock", "MagnetBlock", "FurBlock", "LeatherBlock", "SaddleBlock", "ChristmasTreeBlock",
			"FurnitureBlock", "TreasureGeneratorBlock", "CairnBlock",
            
            // Clothes (Se procesan de forma especial abajo)
            "ClothingBlock",
            
            // Tools
            "StoneAxeBlock", "CopperAxeBlock", "IronAxeBlock", "DiamondAxeBlock", "CopperShovelBlock", "IronShovelBlock",
			"DiamondShovelBlock", "CopperPickaxeBlock", "IronPickaxeBlock", "DiamondPickaxeBlock", "WoodenRakeBlock",
			"CopperRakeBlock", "IronRakeBlock", "DiamondRakeBlock", "EmptyBucketBlock", "WaterBucketBlock",
			"MagmaBucketBlock", "PaintStripperBucketBlock", "PaintBucketBlock", "MatchBlock", "CompassBlock",
			"ThermometerBlock", "HygrometerBlock", "IronHammerBlock", "WhistleBlock",
            
            // Weapons
            "WoodenClubBlock", "StoneClubBlock", "WoodenSpearBlock", "StoneSpearBlock", "CopperSpearBlock",
			"IronSpearBlock", "DiamondSpearBlock", "CopperMacheteBlock", "IronMacheteBlock", "DiamondMacheteBlock",
			"BowBlock", "CrossbowBlock", "MusketBlock", "ArrowBlock", "BulletBlock", "BombBlock", "IncendiaryBombBlock",
            
            // Fireworks
            "FireworksBlock",
            
            // Food
            "FlourBlock", "DoughBlock", "RottenDoughBlock", "BreadBlock", "RottenBreadBlock", "RawBirdBlock",
			"CookedBirdBlock", "RottenBirdBlock", "RawMeatBlock", "CookedMeatBlock", "RottenMeatBlock", "RawFishBlock",
			"CookedFishBlock", "RottenFishBlock", "MilkBucketBlock", "RottenMilkBucketBlock", "RottenEggBlock",
			"PumpkinSoupBucketBlock", "RottenPumpkinSoupBucketBlock",
            
            // Electrics
            "WireBlock", "NotGateBlock", "AndGateBlock", "OrGateBlock", "XorGateBlock", "NandGateBlock", "NorGateBlock",
			"DelayGateBlock", "AdjustableDelayGateBlock", "SRLatchBlock", "CounterBlock", "MemoryBankBlock",
			"TruthTableCircuitBlock", "RealTimeClockBlock", "RandomGeneratorBlock", "DigitalToAnalogConverterBlock",
			"AnalogToDigitalConverterBlock", "SoundGeneratorBlock", "BatteryBlock", "LightbulbBlock", "SwitchBlock",
			"ButtonBlock", "PressurePlateBlock", "MotionDetectorBlock", "PistonBlock", "PistonHeadBlock",
			"WireThroughPlanksBlock", "WireThroughStoneBlock", "WireThroughBricksBlock", "WireThroughSemiconductorBlock",
			"WireThroughCobblestoneBlock", "LedBlock", "FourLedBlock", "SevenSegmentDisplayBlock", "MulticoloredLedBlock",
			"OneLedBlock", "PhotodiodeBlock", "TargetBlock", "DetonatorBlock"
		};

		private List<int> m_validGiftBlocks;

		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			// 100% de probabilidad igual que el huevo "no laid" original
			if (this.m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative || this.m_random.Float(0f, 1f) <= 1f)
			{
				try
				{
					// --- SORTEO DE EXPERIENCIA (ÓRBITAS) ---
					int expCount = RollExperienceCount();
					if (expCount > 0)
					{
						Block expBlock = BlocksManager.GetBlock("ExperienceBlock");
						if (expBlock != null)
						{
							int expValue = Terrain.MakeBlockValue(expBlock.BlockIndex, 0, 0);
							Vector3 expVelocity = new Vector3(
								m_random.Float(-1.5f, 1.5f),
								m_random.Float(2f, 4f),
								m_random.Float(-1.5f, 1.5f)
							);
							m_subsystemPickables.AddPickable(expValue, expCount, worldItem.Position, expVelocity, null);
						}
					}

					// --- SOLTAR LOS OBJETOS DE REGALO ---
					int giftCount = RollGiftCount();
					List<int> validBlocks = GetValidGiftBlocks();

					if (validBlocks != null && validBlocks.Count > 0)
					{
						for (int i = 0; i < giftCount; i++)
						{
							int randomIdx = MathUtils.Clamp(
								(int)Math.Floor(m_random.Float(0f, (float)validBlocks.Count)),
								0,
								validBlocks.Count - 1
							);
							int blockIndex = validBlocks[randomIdx];

							int blockValue = Terrain.MakeBlockValue(blockIndex, 0, 0);

							// Si el bloque es ropa (ClothingBlock), tiene otro proceso:
							// Le asignamos un índice de prenda aleatorio en su Data para que no salga vacío.
							if (m_clothingBlock != null && blockIndex == m_clothingBlock.BlockIndex)
							{
								int randomClothingIdx = s_clothingIndices[(int)m_random.Float(0f, s_clothingIndices.Length)];
								int clothingData = ClothingBlock.SetClothingIndex(0, randomClothingIdx);
								blockValue = Terrain.MakeBlockValue(blockIndex, 0, clothingData);
							}

							// Velocidad aleatoria para dispersar los objetos
							Vector3 velocity = new Vector3(
								m_random.Float(-2.5f, 2.5f),
								m_random.Float(2f, 4.5f),
								m_random.Float(-2.5f, 2.5f)
							);

							// Agregar el objeto usando SubsystemPickables
							m_subsystemPickables.AddPickable(blockValue, 1, worldItem.Position, velocity, null);
						}
					}
				}
				catch (Exception value)
				{
					DefaultInterpolatedStringHandler handler = new DefaultInterpolatedStringHandler(40, 1);
					handler.AppendLiteral("Spawning gifts from Christmas egg error: ");
					handler.AppendFormatted<Exception>(value);
					Log.Error(handler.ToStringAndClear());

					Projectile projectile = worldItem as Projectile;
					if (projectile != null)
					{
						ComponentCreature owner = projectile.Owner;
						ComponentGui componentGui = (owner != null) ? owner.Entity.FindComponent<ComponentGui>() : null;
						if (componentGui != null)
						{
							componentGui.DisplaySmallMessage(
								LanguageControl.Get(new string[]
								{
									"SubsystemChristmasGiftEggBlockBehavior",
									"1"
								}),
								Color.White, true, false
							);
						}
					}
				}
			}
			return true;
		}

		private int RollGiftCount()
		{
			float roll = m_random.Float(0f, 1f);
			float cumulative = 0f;
			for (int i = 0; i < s_giftCounts.Length; i++)
			{
				cumulative += s_giftProbabilities[i];
				if (roll <= cumulative)
				{
					return s_giftCounts[i];
				}
			}
			return 1;
		}

		private int RollExperienceCount()
		{
			float roll = m_random.Float(0f, 1f);
			float cumulative = 0f;
			for (int i = 0; i < s_expCounts.Length; i++)
			{
				cumulative += s_expProbabilities[i];
				if (roll <= cumulative)
				{
					return s_expCounts[i];
				}
			}
			return 0;
		}

		private List<int> GetValidGiftBlocks()
		{
			if (m_validGiftBlocks != null)
			{
				return m_validGiftBlocks;
			}

			m_validGiftBlocks = new List<int>();

			// Iterar sobre la lista explícita de bloques definida en el código (sacada del TXT)
			foreach (string blockName in s_validBlockNames)
			{
				Block block = BlocksManager.GetBlock(blockName);
				if (block != null)
				{
					m_validGiftBlocks.Add(block.BlockIndex);
				}
			}

			return m_validGiftBlocks;
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_subsystemPickables = base.Project.FindSubsystem<SubsystemPickables>(true);
			this.m_subsystemCreatureSpawn = base.Project.FindSubsystem<SubsystemCreatureSpawn>(true);

			// Usar BlocksManager con "" para obtener referencias
			this.m_clothingBlock = BlocksManager.GetBlock("ClothingBlock") as ClothingBlock;
		}

		public const string fName = "SubsystemChristmasGiftEggBlockBehavior";
	}
}
