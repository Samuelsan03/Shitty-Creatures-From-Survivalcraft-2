using System;
using System.Collections.Generic;
using System.Linq;
using Engine;
using Engine.Graphics;
using Engine.Serialization;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentCreatureClothing : Component, IUpdateable, IInventory
	{
		public float SteedMovementSpeedFactor { get; private set; }

		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		Project IInventory.Project
		{
			get
			{
				return base.Project;
			}
		}

		public int SlotsCount
		{
			get
			{
				return 4;
			}
		}

		public int ActiveSlotIndex
		{
			get
			{
				return -1;
			}
			set
			{
			}
		}

		public int VisibleSlotsCount
		{
			get
			{
				return this.SlotsCount;
			}
			set
			{
			}
		}

		public ReadOnlyList<int> GetClothes(ClothingSlot slot)
		{
			return new ReadOnlyList<int>(this.m_clothes[slot]);
		}

		public virtual int GetClothesIndex(ClothingSlot slot, string displayName, out List<int> list)
		{
			int result = -1;
			list = new List<int>(this.GetClothes(slot));
			for (int i = list.Count - 1; i >= 0; i--)
			{
				int value = list[i];
				Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
				ClothingBlock clothingBlock = block as ClothingBlock;
				bool flag = clothingBlock != null && clothingBlock.GetDisplayName(this.m_subsystemTerrain, value) == displayName;
				if (flag)
				{
					result = i;
					break;
				}
			}
			return result;
		}

		public virtual void SetClothes(ClothingSlot slot, IEnumerable<int> clothes)
		{
			bool flag = !this.m_clothes[slot].SequenceEqual(clothes);
			if (flag)
			{
				this.m_clothes[slot].Clear();
				this.m_clothes[slot].AddRange(clothes);
				this.m_clothedTexturesValid = false;
				float num = 0f;
				foreach (KeyValuePair<ClothingSlot, List<int>> keyValuePair in this.m_clothes)
				{
					foreach (int value in keyValuePair.Value)
					{
						ClothingData clothingData = this.clothingBlock.GetClothingData(value);
						num += clothingData.DensityModifier;
					}
				}
				float num2 = num - this.m_densityModifierApplied;
				this.m_densityModifierApplied += num2;
				this.m_componentBody.Density += num2;
				this.SteedMovementSpeedFactor = 1f;
				foreach (int value2 in this.GetClothes(ClothingSlot.Head))
				{
					ClothingData clothingData2 = this.clothingBlock.GetClothingData(value2);
					this.SteedMovementSpeedFactor *= clothingData2.SteedMovementSpeedFactor;
				}
				foreach (int value3 in this.GetClothes(ClothingSlot.Torso))
				{
					ClothingData clothingData3 = this.clothingBlock.GetClothingData(value3);
					this.SteedMovementSpeedFactor *= clothingData3.SteedMovementSpeedFactor;
				}
				foreach (int value4 in this.GetClothes(ClothingSlot.Legs))
				{
					ClothingData clothingData4 = this.clothingBlock.GetClothingData(value4);
					this.SteedMovementSpeedFactor *= clothingData4.SteedMovementSpeedFactor;
				}
				foreach (int value5 in this.GetClothes(ClothingSlot.Feet))
				{
					ClothingData clothingData5 = this.clothingBlock.GetClothingData(value5);
					this.SteedMovementSpeedFactor *= clothingData5.SteedMovementSpeedFactor;
				}
			}
		}

		public virtual float ApplyArmorProtection(float attackPower, bool Applied = false)
		{
			bool flag = !Applied;
			if (flag)
			{
				float num = this.m_random.Float(0f, 1f);
				ClothingSlot slot = ((double)num < 0.10000000149011612) ? ClothingSlot.Feet : (((double)num < 0.30000001192092896) ? ClothingSlot.Legs : (((double)num < 0.8999999761581421) ? ClothingSlot.Torso : ClothingSlot.Head));
				float num2 = (float)(BlocksManager.Blocks[203].Durability + 1);
				List<int> list = new List<int>(this.GetClothes(slot));
				for (int i = 0; i < list.Count; i++)
				{
					int value = list[i];
					ClothingData clothingData = BlocksManager.Blocks[Terrain.ExtractContents(value)].GetClothingData(value);
					float x = (num2 - (float)BlocksManager.Blocks[203].GetDamage(value)) / num2 * clothingData.Sturdiness;
					float num3 = MathUtils.Min(attackPower * MathUtils.Saturate(clothingData.ArmorProtection), x);
					bool flag2 = (double)num3 > 0.0;
					if (flag2)
					{
						attackPower -= num3;
						float x2 = (float)((double)num3 / (double)clothingData.Sturdiness * (double)num2 + 0.001);
						int damageCount = (int)((double)MathUtils.Floor(x2) + (this.m_random.Bool(MathUtils.Remainder(x2, 1f)) ? 1.0 : 0.0));
						list[i] = BlocksManager.DamageItem(value, damageCount, null);
						bool flag3 = !string.IsNullOrEmpty(clothingData.ImpactSoundsFolder);
						if (flag3)
						{
							this.m_subsystemAudio.PlayRandomSound(clothingData.ImpactSoundsFolder, 1f, this.m_random.Float(-0.3f, 0.3f), this.m_componentBody.Position, 4f, 0.15f);
						}
					}
				}
				int j = 0;
				while (j < list.Count)
				{
					bool flag4 = Terrain.ExtractContents(list[j]) != 203;
					if (flag4)
					{
						list.RemoveAt(j);
						this.m_subsystemParticles.AddParticleSystem(new BlockDebrisParticleSystem(this.m_subsystemTerrain, this.m_componentBody.Position + this.m_componentBody.StanceBoxSize / 2f, 1f, 1f, Color.White, 0), false);
					}
					else
					{
						j++;
					}
				}
				this.SetClothes(slot, list);
			}
			return MathUtils.Max(attackPower, 0f);
		}

		public virtual float NewApplyArmorProtection(float attackPower, float armorPenetration)
		{
			bool flag = false;
			bool flag2 = !flag;
			if (flag2)
			{
				float num = this.m_random.Float(0f, 1f);
				ClothingSlot slot = (num < 0.1f) ? ClothingSlot.Feet : ((num < 0.3f) ? ClothingSlot.Legs : ((num < 0.9f) ? ClothingSlot.Torso : ClothingSlot.Head));
				float num2 = (float)(BlocksManager.Blocks[203].Durability + 1);
				List<int> list = new List<int>(this.GetClothes(slot));
				for (int i = 0; i < list.Count; i++)
				{
					int value = list[i];
					ClothingData clothingData = BlocksManager.Blocks[Terrain.ExtractContents(value)].GetClothingData(value);
					float num3 = clothingData.ArmorProtection * (1f - MathUtils.Saturate(armorPenetration));
					float x = (num2 - (float)BlocksManager.Blocks[203].GetDamage(value)) / num2 * clothingData.Sturdiness;
					float num4 = MathUtils.Min(attackPower * num3, x);
					bool flag3 = num4 > 0f;
					if (flag3)
					{
						attackPower -= num4;
						float x2 = num4 / clothingData.Sturdiness * num2 + 0.001f;
						int damageCount = (int)(MathUtils.Floor(x2) + (float)(this.m_random.Bool(MathUtils.Remainder(x2, 1f)) ? 1 : 0));
						list[i] = BlocksManager.DamageItem(value, damageCount, null);
						bool flag4 = !string.IsNullOrEmpty(clothingData.ImpactSoundsFolder);
						if (flag4)
						{
							this.m_subsystemAudio.PlayRandomSound(clothingData.ImpactSoundsFolder, 1f, this.m_random.Float(-0.3f, 0.3f), this.m_componentBody.Position, 4f, 0.15f);
						}
					}
				}
				int j = 0;
				while (j < list.Count)
				{
					bool flag5 = Terrain.ExtractContents(list[j]) != 203;
					if (flag5)
					{
						list.RemoveAt(j);
						this.m_subsystemParticles.AddParticleSystem(new BlockDebrisParticleSystem(this.m_subsystemTerrain, this.m_componentBody.Position + this.m_componentBody.StanceBoxSize / 2f, 1f, 1f, Color.White, 0), false);
					}
					else
					{
						j++;
					}
				}
				this.SetClothes(slot, list);
			}
			return MathUtils.Max(attackPower, 0f);
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			this.m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			this.m_componentHumanModel = base.Entity.FindComponent<ComponentHumanModel>(true);
			this.m_componentBody = base.Entity.FindComponent<ComponentBody>(true);
			this.m_componentOuterClothingModel = base.Entity.FindComponent<ComponentOuterClothingModel>(true);

			this.SteedMovementSpeedFactor = 1f;
			this.m_clothes[ClothingSlot.Head] = new List<int>();
			this.m_clothes[ClothingSlot.Torso] = new List<int>();
			this.m_clothes[ClothingSlot.Legs] = new List<int>();
			this.m_clothes[ClothingSlot.Feet] = new List<int>();

			ValuesDictionary clothesDict = valuesDictionary.GetValue<ValuesDictionary>("CreatureClothes");
			string head = clothesDict.GetValue<string>("Head");
			string torso = clothesDict.GetValue<string>("Torso");
			string legs = clothesDict.GetValue<string>("Legs");
			string feet = clothesDict.GetValue<string>("Feet");

			this.SetClothes(ClothingSlot.Head, HumanReadableConverter.ValuesListFromString<int>(';', head));
			this.SetClothes(ClothingSlot.Torso, HumanReadableConverter.ValuesListFromString<int>(';', torso));
			this.SetClothes(ClothingSlot.Legs, HumanReadableConverter.ValuesListFromString<int>(';', legs));
			this.SetClothes(ClothingSlot.Feet, HumanReadableConverter.ValuesListFromString<int>(';', feet));

			// ========== CORRECCIÓN: Obtener textura del template usando el ValuesDictionary ==========
			string texturePath = null;

			// Buscar el parámetro TextureOverride en el ValuesDictionary del Entity
			if (valuesDictionary.Values != null)
			{
				foreach (var kvp in valuesDictionary.Values)
				{
					var dict = kvp as ValuesDictionary;
					if (dict != null && dict.DatabaseObject != null && dict.DatabaseObject.Name == "HumanModel")
					{
						// Buscar el parámetro TextureOverride dentro del HumanModel
						if (dict.Values != null)
						{
							foreach (var paramKvp in dict.Values)
							{
								var paramDict = paramKvp as ValuesDictionary;
								if (paramDict != null && paramDict.DatabaseObject != null && paramDict.DatabaseObject.Name == "TextureOverride")
								{
									texturePath = paramDict.GetValue<string>("Value");
									break;
								}
							}
						}
						break;
					}
				}
			}

			// Si no se encontró en el template, usar la textura del modelo
			if (string.IsNullOrEmpty(texturePath) && m_componentHumanModel != null)
			{
				texturePath = "Textures/Claude Speed";
			}

			// Cargar la textura personalizada
			if (!string.IsNullOrEmpty(texturePath))
			{
				try
				{
					m_baseSkinTexture = ContentManager.Get<Texture2D>(texturePath);
					m_skinTexture = m_baseSkinTexture;

					// Asignar la textura al modelo humano
					if (m_componentHumanModel != null && m_baseSkinTexture != null)
					{
						m_componentHumanModel.TextureOverride = m_baseSkinTexture;
					}
				}
				catch (Exception)
				{
					// Si falla la carga, usar textura por defecto
					LoadDefaultSkin();
				}
			}
			else
			{
				LoadDefaultSkin();
			}
			// ========== FIN CORRECCIÓN ==========

			Display.DeviceReset += this.Display_DeviceReset;
		}

		private void LoadDefaultSkin()
		{
			PlayerData playerData = new PlayerData(base.Project);
			m_baseSkinTexture = CharacterSkinsManager.LoadTexture(playerData.CharacterSkinName);
			m_skinTexture = m_baseSkinTexture;
			if (m_componentHumanModel != null)
			{
				m_componentHumanModel.TextureOverride = m_baseSkinTexture;
			}
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			ValuesDictionary clothesDict = new ValuesDictionary();
			valuesDictionary.SetValue<ValuesDictionary>("CreatureClothes", clothesDict);
			clothesDict.SetValue<string>("Head", HumanReadableConverter.ValuesListToString<int>(';', this.m_clothes[ClothingSlot.Head].ToArray()));
			clothesDict.SetValue<string>("Torso", HumanReadableConverter.ValuesListToString<int>(';', this.m_clothes[ClothingSlot.Torso].ToArray()));
			clothesDict.SetValue<string>("Legs", HumanReadableConverter.ValuesListToString<int>(';', this.m_clothes[ClothingSlot.Legs].ToArray()));
			clothesDict.SetValue<string>("Feet", HumanReadableConverter.ValuesListToString<int>(';', this.m_clothes[ClothingSlot.Feet].ToArray()));
		}

		public sealed override void Dispose()
		{
			base.Dispose();
			bool flag = this.m_skinTexture != null && !ContentManager.IsContent(this.m_skinTexture);
			if (flag)
			{
				this.m_skinTexture.Dispose();
				this.m_skinTexture = null;
			}
			bool flag2 = this.m_innerClothedTexture != null;
			if (flag2)
			{
				this.m_innerClothedTexture.Dispose();
				this.m_innerClothedTexture = null;
			}
			bool flag3 = this.m_outerClothedTexture != null;
			if (flag3)
			{
				this.m_outerClothedTexture.Dispose();
				this.m_outerClothedTexture = null;
			}
			Display.DeviceReset -= this.Display_DeviceReset;
		}

		public virtual void Update(float dt)
		{
			this.UpdateRenderTargets();
		}

		public virtual int GetSlotValue(int slotIndex)
		{
			return this.GetClothes((ClothingSlot)slotIndex).LastOrDefault<int>();
		}

		public virtual int GetSlotCount(int slotIndex)
		{
			bool flag = this.GetClothes((ClothingSlot)slotIndex).Count <= 0;
			int result;
			if (flag)
			{
				result = 0;
			}
			else
			{
				result = 1;
			}
			return result;
		}

		public virtual int GetSlotCapacity(int slotIndex, int value)
		{
			return 0;
		}

		public virtual int GetSlotProcessCapacity(int slotIndex, int value)
		{
			Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
			bool flag = block.GetNutritionalValue(value) > 0f;
			int result;
			if (flag)
			{
				result = 1;
			}
			else
			{
				bool flag2 = !(block is ClothingBlock) || !this.CanWearClothing(value);
				if (flag2)
				{
					result = 0;
				}
				else
				{
					result = 1;
				}
			}
			return result;
		}

		public virtual void AddSlotItems(int slotIndex, int value, int count)
		{
		}

		public virtual void ProcessSlotItems(int slotIndex, int value, int count, int processCount, out int processedValue, out int processedCount)
		{
			processedCount = 0;
			processedValue = 0;
			bool flag = processCount == 1;
			if (flag)
			{
				Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
				bool flag2 = block.GetNutritionalValue(value) > 0f;
				if (flag2)
				{
					bool flag3 = block is BucketBlock;
					if (flag3)
					{
						processedValue = Terrain.MakeBlockValue(90, 0, Terrain.ExtractData(value));
						processedCount = 1;
					}
					bool flag4 = count > 1 && processedCount > 0 && processedValue != value;
					if (flag4)
					{
						processedValue = value;
						processedCount = processCount;
					}
				}
				bool flag5 = block is ClothingBlock;
				if (flag5)
				{
					ClothingData clothingData = this.clothingBlock.GetClothingData(value);
					List<int> clothes = new List<int>(this.GetClothes(clothingData.Slot))
					{
						value
					};
					this.SetClothes(clothingData.Slot, clothes);
				}
			}
		}

		public int RemoveSlotItems(int slotIndex, int count)
		{
			bool flag = count == 1;
			if (flag)
			{
				List<int> list = new List<int>(this.GetClothes((ClothingSlot)slotIndex));
				bool flag2 = list.Count > 0;
				if (flag2)
				{
					list.RemoveAt(list.Count - 1);
					this.SetClothes((ClothingSlot)slotIndex, list);
					return 1;
				}
			}
			return 0;
		}

		public virtual void DropAllItems(Vector3 position)
		{
			Game.Random random = new Game.Random();
			SubsystemPickables subsystemPickables = base.Project.FindSubsystem<SubsystemPickables>(true);
			for (int i = 0; i < this.SlotsCount; i++)
			{
				int slotCount = this.GetSlotCount(i);
				bool flag = slotCount > 0;
				if (flag)
				{
					int slotValue = this.GetSlotValue(i);
					int count = this.RemoveSlotItems(i, slotCount);
					Vector3 value = random.Float(5f, 10f) * Vector3.Normalize(new Vector3(random.Float(-1f, 1f), random.Float(1f, 2f), random.Float(-1f, 1f)));
					subsystemPickables.AddPickable(slotValue, count, position, new Vector3?(value), null, base.Entity);
				}
			}
		}

		private void Display_DeviceReset()
		{
			this.m_clothedTexturesValid = false;
		}

		private bool CanWearClothing(int value)
		{
			ClothingData clothingData = this.clothingBlock.GetClothingData(value);
			IList<int> list = this.GetClothes(clothingData.Slot);
			bool flag = list.Count == 0;
			bool result;
			if (flag)
			{
				result = true;
			}
			else
			{
				ClothingData clothingData2 = this.clothingBlock.GetClothingData(list[list.Count - 1]);
				result = (clothingData.Layer > clothingData2.Layer);
			}
			return result;
		}

		private void UpdateRenderTargets()
		{
			if (m_baseSkinTexture == null)
			{
				LoadDefaultSkin();
				Utilities.Dispose<RenderTarget2D>(ref m_innerClothedTexture);
				Utilities.Dispose<RenderTarget2D>(ref m_outerClothedTexture);
			}

			bool flag2 = this.m_innerClothedTexture == null || this.m_innerClothedTexture.Width != this.m_baseSkinTexture.Width || this.m_innerClothedTexture.Height != this.m_baseSkinTexture.Height;
			if (flag2)
			{
				this.m_innerClothedTexture = new RenderTarget2D(this.m_baseSkinTexture.Width, this.m_baseSkinTexture.Height, 1, ColorFormat.Rgba8888, DepthFormat.None);
				this.m_componentHumanModel.TextureOverride = this.m_innerClothedTexture;
				this.m_clothedTexturesValid = false;
			}
			bool flag3 = this.m_outerClothedTexture == null || this.m_outerClothedTexture.Width != this.m_baseSkinTexture.Width || this.m_outerClothedTexture.Height != this.m_baseSkinTexture.Height;
			if (flag3)
			{
				this.m_outerClothedTexture = new RenderTarget2D(this.m_baseSkinTexture.Width, this.m_baseSkinTexture.Height, 1, ColorFormat.Rgba8888, DepthFormat.None);
				this.m_componentOuterClothingModel.TextureOverride = this.m_outerClothedTexture;
				this.m_clothedTexturesValid = false;
			}
			bool flag4 = ComponentCreatureClothing.DrawClothedTexture && !this.m_clothedTexturesValid;
			if (flag4)
			{
				this.m_clothedTexturesValid = true;
				Rectangle scissorRectangle = Display.ScissorRectangle;
				RenderTarget2D renderTarget = Display.RenderTarget;
				try
				{
					Display.RenderTarget = this.m_innerClothedTexture;
					Display.Clear(new Vector4?(new Vector4(Color.Transparent)), null, null);
					int num = 0;
					TexturedBatch2D texturedBatch2D = this.m_primitivesRenderer.TexturedBatch(this.m_baseSkinTexture, false, num++, DepthStencilState.None, null, BlendState.NonPremultiplied, SamplerState.PointClamp);
					texturedBatch2D.QueueQuad(Vector2.Zero, new Vector2((float)this.m_innerClothedTexture.Width, (float)this.m_innerClothedTexture.Height), 0f, Vector2.Zero, Vector2.One, Color.White);
					foreach (ClothingSlot slot in ComponentCreatureClothing.m_innerSlotsOrder)
					{
						foreach (int value in this.GetClothes(slot))
						{
							int data = Terrain.ExtractData(value);
							ClothingData clothingData = this.clothingBlock.GetClothingData(value);
							Color fabricColor = SubsystemPalette.GetFabricColor(this.m_subsystemTerrain, new int?(ClothingBlock.GetClothingColor(data)));
							texturedBatch2D = this.m_primitivesRenderer.TexturedBatch(clothingData.Texture, false, num++, DepthStencilState.None, null, BlendState.NonPremultiplied, SamplerState.PointClamp);
							bool flag5 = !clothingData.IsOuter;
							if (flag5)
							{
								texturedBatch2D.QueueQuad(new Vector2(0f, 0f), new Vector2((float)this.m_innerClothedTexture.Width, (float)this.m_innerClothedTexture.Height), 0f, Vector2.Zero, Vector2.One, fabricColor);
							}
						}
					}
					this.m_primitivesRenderer.Flush(true, int.MaxValue);
					Display.RenderTarget = this.m_outerClothedTexture;
					Display.Clear(new Vector4?(new Vector4(Color.Transparent)), null, null);
					num = 0;
					foreach (ClothingSlot slot2 in ComponentCreatureClothing.m_outerSlotsOrder)
					{
						foreach (int value2 in this.GetClothes(slot2))
						{
							int data2 = Terrain.ExtractData(value2);
							ClothingData clothingData2 = this.clothingBlock.GetClothingData(value2);
							Color fabricColor2 = SubsystemPalette.GetFabricColor(this.m_subsystemTerrain, new int?(ClothingBlock.GetClothingColor(data2)));
							texturedBatch2D = this.m_primitivesRenderer.TexturedBatch(clothingData2.Texture, false, num++, DepthStencilState.None, null, BlendState.NonPremultiplied, SamplerState.PointClamp);
							bool isOuter = clothingData2.IsOuter;
							if (isOuter)
							{
								float num2 = 1f;
								bool flag6 = num2 < 1f;
								if (flag6)
								{
									fabricColor2.A = (byte)(num2 * 255f);
								}
								texturedBatch2D.QueueQuad(new Vector2(0f, 0f), new Vector2((float)this.m_outerClothedTexture.Width, (float)this.m_outerClothedTexture.Height), 0f, Vector2.Zero, Vector2.One, fabricColor2);
							}
						}
					}
					this.m_primitivesRenderer.Flush(true, int.MaxValue);
				}
				finally
				{
					Display.RenderTarget = renderTarget;
					Display.ScissorRectangle = scissorRectangle;
				}
			}
		}

		public static int GetClothingSlotIndex(ClothingSlot slot)
		{
			if (slot == ClothingSlot.Head) return 0;
			if (slot == ClothingSlot.Torso) return 1;
			if (slot == ClothingSlot.Legs) return 2;
			if (slot == ClothingSlot.Feet) return 3;
			return -1;
		}

		public SubsystemAudio m_subsystemAudio;
		public SubsystemParticles m_subsystemParticles;
		private SubsystemGameInfo m_subsystemGameInfo;
		private readonly ClothingBlock clothingBlock = BlocksManager.Blocks[203] as ClothingBlock;
		private SubsystemTerrain m_subsystemTerrain;
		private ComponentHumanModel m_componentHumanModel;
		private ComponentBody m_componentBody;
		private ComponentOuterClothingModel m_componentOuterClothingModel;
		private Texture2D m_skinTexture;
		private Texture2D m_baseSkinTexture;
		private RenderTarget2D m_innerClothedTexture;
		private RenderTarget2D m_outerClothedTexture;
		private readonly PrimitivesRenderer2D m_primitivesRenderer = new PrimitivesRenderer2D();
		private readonly Game.Random m_random = new Game.Random();
		private float m_densityModifierApplied;
		private bool m_clothedTexturesValid;
		private readonly List<int> m_clothesList = new List<int>();
		private readonly Dictionary<ClothingSlot, List<int>> m_clothes = new Dictionary<ClothingSlot, List<int>>();

		private static readonly ClothingSlot[] m_innerSlotsOrder = new ClothingSlot[]
		{
			ClothingSlot.Head,
			ClothingSlot.Torso,
			ClothingSlot.Feet,
			ClothingSlot.Legs
		};

		private static readonly ClothingSlot[] m_outerSlotsOrder = new ClothingSlot[]
		{
			ClothingSlot.Head,
			ClothingSlot.Torso,
			ClothingSlot.Legs,
			ClothingSlot.Feet
		};

		public static bool ShowClothedTexture = false;
		public static bool DrawClothedTexture = true;
	}
}
