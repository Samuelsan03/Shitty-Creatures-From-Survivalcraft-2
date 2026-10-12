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

		// Plan de probabilidad (cantidad de regalos sueltos)
		private static readonly int[] s_giftCounts = { 1, 2, 3, 4, 5, 6, 7, 8 };
		private static readonly float[] s_giftProbabilities = { 0.08f, 0.18f, 0.28f, 0.24f, 0.12f, 0.06f, 0.03f, 0.01f };

		// Plan de probabilidad de órbitas de experiencia
		private static readonly int[] s_expCounts = { 0, 1, 3, 5 };
		private static readonly float[] s_expProbabilities = { 0.60f, 0.25f, 0.10f, 0.05f };

		// =====================================================================
		//  NUEVO: Plan de probabilidad para el STACKING (cantidad de items por regalo)
		//  Se sortea para CADA regalo individualmente.
		//  El stack lleno respeta el MaxStacking del CSV (40, 100000, etc.)
		// =====================================================================
		private static readonly int[] s_stackAmounts = { 1, 4, 12, 40, -1 }; // -1 = Stack lleno (MaxStacking)
		private static readonly float[] s_stackProbabilities = { 0.35f, 0.25f, 0.20f, 0.15f, 0.05f };

		private static readonly int[] s_clothingIndices =
		{
			0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
			16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29,
			30, 31, 32, 33, 34, 35, 36, 37
		};

		private static readonly string[] s_validBlockNames = new string[]
		{
            // ===================== Terrain =====================
            "BedrockBlock", "DirtBlock", "SoilBlock", "LimestoneBlock", "GraniteBlock", "BasaltBlock",
			"SandstoneBlock", "GravelBlock", "ClayBlock", "SandBlock", "GrassBlock", "SnowBlock",
			"IceBlock", "StoneChunkBlock",

            // ===================== Minerals =====================
            "CoalOreBlock", "CopperOreBlock", "IronOreBlock", "SulphurOreBlock", "DiamondOreBlock",
			"GermaniumOreBlock", "SaltpeterOreBlock", "CoalBlock", "CoalChunkBlock", "MalachiteChunkBlock",
			"IronOreChunkBlock", "SaltpeterChunkBlock", "SulphurChunkBlock", "GermaniumOreChunkBlock",
			"GermaniumChunkBlock", "DiamondChunkBlock",

            // ===================== Plants (vanilla) =====================
            "OakWoodBlock", "BirchWoodBlock", "SpruceWoodBlock", "MimosaWoodBlock", "PoplarWoodBlock",
			"FallenLeavesBlock", "OakLeavesBlock", "BirchLeavesBlock", "SpruceLeavesBlock",
			"TallSpruceLeavesBlock", "MimosaLeavesBlock", "PoplarLeavesBlock", "TallGrassBlock", "RyeBlock",
			"RedFlowerBlock", "PurpleFlowerBlock", "WhiteFlowerBlock", "DryBushBlock", "LargeDryBushBlock",
			"SaplingBlock", "CactusBlock", "IvyBlock", "PumpkinBlock", "RottenPumpkinBlock", "SeedsBlock",
			"CottonBlock", "CottonWadBlock", "SeaUrchinBlock", "StarfishBlock", "KelpBlock", "SeagrassBlock",

            // ===================== Plants (frutales nuevos) =====================
            "AppleWoodBlock", "AppleLeavesBlock", "AppleSaplingBlock",
			"PearWoodBlock", "PearLeavesBlock", "PearSaplingBlock",
			"OrangeWoodBlock", "OrangeLeavesBlock", "OrangeSaplingBlock",
			"CherryWoodBlock", "CherryLeavesBlock", "CherrySaplingBlock",
			"BananaWoodBlock", "BananaLeavesBlock", "BananaSaplingBlock",
			"BlueberryBushBlock", "BlueberrySeedBlock", "WatermelonSeedBlock",
			"WatermelonBlock", "RottenWatermelonBlock",

            // ===================== Construction =====================
            "PlanksBlock", "CobblestoneBlock", "StoneBrickBlock", "BricksBlock", "MarbleBlock",
			"MalachiteBlock", "DiamondBlock", "IronBlock", "CopperBlock", "SemiconductorBlock",
			"GlassBlock", "FramedGlassBlock", "WindowBlock", "DirtStairsBlock", "WoodenStairsBlock",
			"StoneStairsBlock", "CobblestoneStairsBlock", "StoneBrickStairsBlock", "BrickStairsBlock",
			"SandstoneStairsBlock", "MarbleStairsBlock", "BasaltStairsBlock", "DirtSlabBlock",
			"WoodenSlabBlock", "StoneSlabBlock", "CobblestoneSlabBlock", "StoneBrickSlabBlock",
			"BrickSlabBlock", "SandstoneSlabBlock", "MarbleSlabBlock", "BasaltSlabBlock",

            // ===================== Items (vanilla) =====================
            "BrickBlock", "SnowballBlock", "WoodenDoorBlock", "IronDoorBlock", "CellDoorBlock",
			"WoodenTrapdoorBlock", "CellTrapdoorBlock", "WoodenLadderBlock", "IronLadderBlock",
			"WickerLampBlock", "TorchBlock", "StickBlock", "IronIngotBlock", "CopperIngotBlock",
			"RodBlock", "CraftingTableBlock", "FurnaceBlock", "LitFurnaceBlock", "ChestBlock",
			"DispenserBlock", "SpikedPlankBlock", "GrassTrapBlock", "WoodenFenceBlock",
			"WoodenFenceGateBlock", "IronFenceBlock", "IronFenceGateBlock", "StoneFenceBlock",
			"BrickFenceBlock", "BasaltFenceBlock", "WoodenPostedSignBlock", "IronPostedSignBlock",
			"SmallGunpowderKegBlock", "MediumGunpowderKegBlock", "LargeGunpowderKegBlock",
			"SmallIncendiaryKegBlock", "MediumIncendiaryKegBlock", "LargeIncendiaryKegBlock",
			"GravestoneBlock", "BoatBlock", "FeatherBlock", "StringBlock", "CanvasBlock", "CarpetBlock",
			"CampfireBlock", "GunpowderBlock", "PigmentBlock", "JackOLanternBlock", "MagnetBlock",
			"FurBlock", "LeatherBlock", "SaddleBlock", "ChristmasTreeBlock", "FurnitureBlock",
			"CairnBlock", "FirstAidKitBlock", "AntidotePillBlock", "NuclearCoinBlock",

            // ===================== Items (nuevos del CSV) =====================
            "RefrigeratorXiaomiBlock", "AirConditionerBlock",
			"ToxinBlock", "RemoteControlBlock", "FreezingSnowballBlock",
			"LetterWarBlock", "FarmerWandBlock",

            // ===================== Clothes =====================
            "ClothingBlock",

            // ===================== Tools (vanilla) =====================
            "StoneAxeBlock", "CopperAxeBlock", "IronAxeBlock", "DiamondAxeBlock", "CopperShovelBlock",
			"IronShovelBlock", "DiamondShovelBlock", "CopperPickaxeBlock", "IronPickaxeBlock",
			"DiamondPickaxeBlock", "WoodenRakeBlock", "CopperRakeBlock", "IronRakeBlock",
			"DiamondRakeBlock", "EmptyBucketBlock", "WaterBucketBlock", "MagmaBucketBlock",
			"PaintStripperBucketBlock", "PaintBucketBlock", "MatchBlock", "CompassBlock",
			"ThermometerBlock", "HygrometerBlock", "IronHammerBlock", "WhistleBlock",
			"CollarBlock", "TargetStickBlock",

            // ===================== Tools (nuevos del CSV) =====================
            "LavaShovelBlock", "LavaRakeBlock", "LavaPickaxeBlock", "LavaHammerBlock", "LavaAxeBlock",
			"WoodAxeBlock", "WoodPickaxeBlock", "WoodShovelBlock",
			"StoneShovelBlock", "StoneAxeOriginalBlock", "StoneRakeBlock", "StonePickaxeBlock",
			"CopperThinHammerBlock", "IronThinHammerBlock", "DiamondThinHammerBlock", "LavaThinHammerBlock",

            // ===================== Weapons (vanilla) =====================
            "WoodenClubBlock", "StoneClubBlock", "WoodenSpearBlock", "StoneSpearBlock",
			"CopperSpearBlock", "IronSpearBlock", "DiamondSpearBlock", "CopperMacheteBlock",
			"IronMacheteBlock", "DiamondMacheteBlock", "BowBlock", "CrossbowBlock", "MusketBlock",
			"ArrowBlock", "BulletBlock", "BombBlock", "IncendiaryBombBlock",
			"FreezeBombBlock", "PoisonBombBlock",
			"DoubleMusketBlock", "AntiTanksBulletBlock", "CannonBallBlock", "CannonBlock",
			"FlameBulletBlock", "FlameThrowerBlock", "RepeatArrowBlock", "RepeatCrossbowBlock",

            // ===================== Weapons (nuevos del CSV) =====================
            "WoodenLongspearBlock", "StoneLongspearBlock", "CopperLongspearBlock",
			"IronLongspearBlock", "DiamondLongspearBlock", "LavaLongspearBlock",
			"LavaSpearBlock", "LavaMacheteBlock",
			"WoodMacheteBlock", "StoneMacheteBlock",
			"CopperSharpHammerBlock", "IronSharpHammerBlock", "DiamondSharpHammerBlock", "LavaSharpHammerBlock",
			"ScyteBlock", "TridentBlock", "BaseballBatBlock",

			"AKBlock", "AKBullet",
			"M4Block", "M4Bullet",
			"SPAS12Block", "SPAS12BulletBlock",
			"Mac10Block", "Mac10Bullet",
			"UziBlock", "UziBullet",
			"G3Block", "G3Bullet",
			"MinigunBlock", "MinigunBullet",
			"Izh43Block", "Izh43Bullet",
			"SWM500Block", "SWM500Bullet",
			"SniperBlock", "SniperBullet",
			"AUGBlock", "AUGBullet",
			"P90Block", "P90BulletBlock",
			"SCARBlock", "SCARBulletBlock",
			"RevolverBlock", "RevolverBulletBlock",
			"FamasBlock", "FamasBulletBlock",
			"AA12Block", "AA12BulletBlock",
			"M249Block", "M249BulletBlock",
			"NewG3Block", "NewG3Bullet",
			"MP5SSDBlock", "MP5SSDBulletBlock",
			"MendozaBlock", "MendozaBulletBlock",
			"GrozaBlock", "GrozaBulletBlock",
			"KABlock", "KABulletBlock",
			"BK43Block", "BK43BulletBlock",
			"AK48Block", "AK48BulletBlock",
			"Master308Block", "Master308BulletBlock",

            // ===================== Fireworks =====================
            "FireworksBlock",

            // ===================== Food (vanilla) =====================
            "FlourBlock", "DoughBlock", "RottenDoughBlock", "BreadBlock", "RottenBreadBlock",
			"RawBirdBlock", "CookedBirdBlock", "RottenBirdBlock", "RawMeatBlock", "CookedMeatBlock",
			"RottenMeatBlock", "RawFishBlock", "CookedFishBlock", "RottenFishBlock", "MilkBucketBlock",
			"RottenMilkBucketBlock", "RottenEggBlock", "PumpkinSoupBucketBlock",
			"RottenPumpkinSoupBucketBlock", "AntidoteBucketBlock", "BoiledWaterBucketBlock",
			"BowlBlock",

            // ===================== Food (nuevos del CSV) =====================
            "EmptyBowlBlock", "WaterBowlBlock", "BoiledWaterBowlBlock", "CactusJuiceBowlBlock",
			"AntidoteBowlBlock", "TeaAntifluBowlBlock",
			"TeaAntifluBucketBlock",
			"AppleBlock", "RottenAppleBlock",
			"PearBlock", "RottenPearBlock",
			"OrangeBlock", "RottenOrangeBlock",
			"CherryBlock", "RottenCherryBlock",
			"BananaBlock", "RottenBananaBlock",
			"BlueberryBlock", "RottenBlueberryBlock",
			"SliceOfWatermelonBlock", "RottenSliceOfWatermelonBlock",

            // ===================== Spawner Eggs (nuevos) =====================
            "InfectedSpawnEggBlock", "BanditEggBlock",

            // ===================== Electrics =====================
            "WireBlock", "NotGateBlock", "AndGateBlock", "OrGateBlock", "XorGateBlock", "NandGateBlock",
			"NorGateBlock", "DelayGateBlock", "AdjustableDelayGateBlock", "SRLatchBlock", "CounterBlock",
			"MemoryBankBlock", "TruthTableCircuitBlock", "RealTimeClockBlock", "RandomGeneratorBlock",
			"DigitalToAnalogConverterBlock", "AnalogToDigitalConverterBlock", "SoundGeneratorBlock",
			"BatteryBlock", "LightbulbBlock", "SwitchBlock", "ButtonBlock", "PressurePlateBlock",
			"MotionDetectorBlock", "PistonBlock", "PistonHeadBlock", "WireThroughPlanksBlock",
			"WireThroughStoneBlock", "WireThroughBricksBlock", "WireThroughSemiconductorBlock",
			"WireThroughCobblestoneBlock", "LedBlock", "FourLedBlock", "SevenSegmentDisplayBlock",
			"MulticoloredLedBlock", "OneLedBlock", "PhotodiodeBlock", "TargetBlock", "DetonatorBlock"
		};

		private List<int> m_validGiftBlocks;
		private readonly Dictionary<int, List<int>> m_creativeValuesCache = new Dictionary<int, List<int>>();

		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
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

							// ★ Obtener el valor del bloque con variante aleatoria (flechas, balas, ropa, etc.)
							int blockValue = GetRandomGiftBlockValue(blockIndex);

							// ★ NUEVO: Sortear la cantidad de items de este regalo
							// Basado en el plan de stacking. Si sale "Stack lleno", lee el MaxStacking del bloque.
							int stackCount = RollStackCount(blockValue);

							Vector3 velocity = new Vector3(
								m_random.Float(-2.5f, 2.5f),
								m_random.Float(2f, 4.5f),
								m_random.Float(-2.5f, 2.5f)
							);

							m_subsystemPickables.AddPickable(blockValue, stackCount, worldItem.Position, velocity, null);
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

		/// <summary>
		/// Sortea cuántos objetos del mismo tipo se soltarán en un regalo.
		/// 35% -> 1 objeto
		/// 25% -> 4 objetos
		/// 20% -> 12 objetos
		/// 15% -> 40 objetos
		/// 5%  -> Stack lleno (según MaxStacking del bloque)
		/// </summary>
		private int RollStackCount(int blockValue)
		{
			float roll = m_random.Float(0f, 1f);
			float cumulative = 0f;
			for (int i = 0; i < s_stackAmounts.Length; i++)
			{
				cumulative += s_stackProbabilities[i];
				if (roll <= cumulative)
				{
					int amount = s_stackAmounts[i];
					// Si el sorteo es -1 (Stack lleno), obtenemos el verdadero MaxStacking del bloque
					if (amount == -1)
					{
						int blockIndex = Terrain.ExtractContents(blockValue);
						if (blockIndex >= 0 && blockIndex < BlocksManager.Blocks.Length)
						{
							Block block = BlocksManager.Blocks[blockIndex];
							int maxStack = block.GetMaxStacking(blockValue);
							// Si el bloque no es apilable (maxStack <= 0), se entrega 1 para no perder el item
							return maxStack > 0 ? maxStack : 1;
						}
						return 1;
					}
					return amount;
				}
			}
			return 1;
		}

		private int GetRandomGiftBlockValue(int blockIndex)
		{
			Block block = BlocksManager.Blocks[blockIndex];

			// --- Ropa (ClothingBlock): lista explícita de índices ---
			if (m_clothingBlock != null && blockIndex == m_clothingBlock.BlockIndex)
			{
				int randomClothingIdx = s_clothingIndices[m_random.Int(0, s_clothingIndices.Length - 1)];
				int clothingData = ClothingBlock.SetClothingIndex(0, randomClothingIdx);
				return Terrain.MakeBlockValue(blockIndex, 0, clothingData);
			}

			// ===================== PLANTAS MADURO FORZADO =====================
			if (block is CottonBlock)
			{
				int data = CottonBlock.SetSize(0, 2);
				data = CottonBlock.SetIsWild(data, false);
				return Terrain.MakeBlockValue(blockIndex, 0, data);
			}
			if (block is RyeBlock)
			{
				int data = RyeBlock.SetSize(0, 7);
				data = RyeBlock.SetIsWild(data, false);
				return Terrain.MakeBlockValue(blockIndex, 0, data);
			}
			if (block is TallGrassBlock)
			{
				int data = TallGrassBlock.SetIsSmall(0, false);
				return Terrain.MakeBlockValue(blockIndex, 0, data);
			}

			// ============== RESTO: GetCreativeValues() aleatorio ==============
			List<int> creativeValues;
			if (!m_creativeValuesCache.TryGetValue(blockIndex, out creativeValues))
			{
				creativeValues = new List<int>();
				foreach (int v in block.GetCreativeValues())
				{
					creativeValues.Add(v);
				}
				m_creativeValuesCache[blockIndex] = creativeValues;
			}

			if (creativeValues.Count > 1)
			{
				return creativeValues[m_random.Int(0, creativeValues.Count - 1)];
			}
			if (creativeValues.Count == 1)
			{
				return creativeValues[0];
			}
			return Terrain.MakeBlockValue(blockIndex, 0, 0);
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
			this.m_clothingBlock = BlocksManager.GetBlock("ClothingBlock") as ClothingBlock;
		}

		public const string fName = "SubsystemChristmasGiftEggBlockBehavior";
	}
}
