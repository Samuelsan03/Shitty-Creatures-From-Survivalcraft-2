using Engine;
using Engine.Graphics;
using Engine.Serialization;
using GameEntitySystem;
using TemplatesDatabase;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game
{
	public class ComponentCreatureClothing : Component, IInventory, IUpdateable
	{
		public Dictionary<ClothingSlot, List<int>> m_clothes = new Dictionary<ClothingSlot, List<int>>();

		public ComponentCreature m_componentCreature;
		public ComponentHumanModel m_componentHumanModel;
		public ComponentNewHumanModel m_componentNewHumanModel;
		public ComponentOuterClothingModel m_componentOuterClothingModel;
		public ComponentBody m_componentBody;
		public ComponentHealth m_componentHealth;
		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemGameInfo m_subsystemGameInfo;
		public SubsystemModelsRenderer m_subsystemModelsRenderer;
		public SubsystemAudio m_subsystemAudio;
		public SubsystemParticles m_subsystemParticles;
		public SubsystemSoundMaterials m_subsystemSoundMaterials;
		public Random m_random = new Random();

		public RenderTarget2D m_innerClothedTexture;
		public RenderTarget2D m_outerClothedTexture;
		public PrimitivesRenderer2D m_primitivesRenderer = new PrimitivesRenderer2D();
		public bool m_clothedTexturesValid;

		// Textura base de la piel, obtenida una sola vez y actualizable si es necesario
		private Texture2D m_cachedSkinTexture;

		public float m_baseDensity;
		public float m_totalDensityModifier;

		Project IInventory.Project => Project;
		public int SlotsCount => 4;
		public int VisibleSlotsCount { get; set; } = 4;
		public int ActiveSlotIndex { get; set; } = -1;
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		private static readonly ClothingSlot[] m_slotsOrder = new[]
		{
			ClothingSlot.Head,
			ClothingSlot.Torso,
			ClothingSlot.Legs,
			ClothingSlot.Feet
		};

		public static int GetClothingSlotIndex(ClothingSlot slot) => Array.IndexOf(m_slotsOrder, slot);

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemModelsRenderer = Project.FindSubsystem<SubsystemModelsRenderer>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemSoundMaterials = Project.FindSubsystem<SubsystemSoundMaterials>(true);

			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentBody = m_componentCreature.ComponentBody;
			m_componentHealth = Entity.FindComponent<ComponentHealth>(true);
			m_componentHumanModel = Entity.FindComponent<ComponentHumanModel>(true);
			m_componentNewHumanModel = Entity.FindComponent<ComponentNewHumanModel>(false);
			m_componentOuterClothingModel = Entity.FindComponent<ComponentOuterClothingModel>(true);

			m_baseDensity = m_componentBody.Density;
			m_totalDensityModifier = 0f;

			// Obtener y cachear la textura de piel base
			m_cachedSkinTexture = m_componentHumanModel.TextureOverride;
			if (m_cachedSkinTexture == null)
			{
				// Intentar obtener del modelo (primer mesh part con TexturePath)
				Model model = m_componentHumanModel.Model;
				if (model != null)
				{
					foreach (ModelMesh mesh in model.Meshes)
					{
						foreach (ModelMeshPart part in mesh.MeshParts)
						{
							if (!string.IsNullOrEmpty(part.TexturePath))
							{
								m_cachedSkinTexture = ContentManager.Get<Texture2D>(part.TexturePath);
								break;
							}
						}
						if (m_cachedSkinTexture != null) break;
					}
				}
			}
			// Si sigue siendo null, no se podrá renderizar (se usará textura por defecto del modelo, no se sobrescribe)

			foreach (ClothingSlot slot in ClothingSlot.ClothingSlots.Values)
				m_clothes[slot] = new List<int>();

			ValuesDictionary clothesData = valuesDictionary.GetValue<ValuesDictionary>("CreatureClothes", null);
			if (clothesData != null)
			{
				foreach (string key in ClothingSlot.ClothingSlots.Keys)
				{
					List<int> loadedClothes = HumanReadableConverter.ValuesListFromString<int>(
						';', clothesData.GetValue<string>(key, "")).ToList();
					m_clothes[ClothingSlot.ClothingSlots[key]] = loadedClothes;
				}
			}

			if (m_componentHealth != null)
				m_componentHealth.Injured += OnInjured;

			Display.DeviceReset += Display_DeviceReset;
			RecalculateDensity();
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			var clothesSave = new ValuesDictionary();
			foreach (var kv in m_clothes)
				clothesSave.SetValue<string>(kv.Key.Name,
					HumanReadableConverter.ValuesListToString<int>(';', kv.Value.ToArray()));
			valuesDictionary.SetValue<ValuesDictionary>("CreatureClothes", clothesSave);
		}

		public override void Dispose()
		{
			base.Dispose();
			Utilities.Dispose(ref m_innerClothedTexture);
			Utilities.Dispose(ref m_outerClothedTexture);
			Display.DeviceReset -= Display_DeviceReset;
			if (m_componentHealth != null)
				m_componentHealth.Injured -= OnInjured;
			// No es necesario restaurar TextureOverride porque al destruir el componente
			// el modelo quedará huérfano de textura, pero el motor lo manejará.
		}

		private void Display_DeviceReset() => m_clothedTexturesValid = false;

		private void OnInjured(Injury injury)
		{
			if (injury == null || m_componentHealth == null || m_componentHealth.Health <= 0f) return;
			if (injury.Attackment != null)
			{
				float remaining = ApplyArmorProtection(injury.Amount);
				injury.Amount = remaining;
			}
		}

		public void SetClothes(ClothingSlot slot, IEnumerable<int> newClothes)
		{
			List<int> clothesList = newClothes.ToList();
			if (!m_clothes[slot].SequenceEqual(clothesList))
			{
				foreach (int oldValue in m_clothes[slot].Except(clothesList))
				{
					ClothingData data = GetClothingData(oldValue);
					data?.Dismount?.Invoke(oldValue, null);
				}
				foreach (int newValue in clothesList.Except(m_clothes[slot]))
				{
					ClothingData data = GetClothingData(newValue);
					data?.Mount?.Invoke(newValue, null);
				}
				m_clothes[slot] = clothesList;
				m_clothedTexturesValid = false;
				RecalculateDensity();
			}
		}

		private void RecalculateDensity()
		{
			float total = 0f;
			foreach (var kv in m_clothes)
				foreach (int v in kv.Value)
				{
					ClothingData d = GetClothingData(v);
					if (d != null) total += d.DensityModifier;
				}
			m_totalDensityModifier = total;
			m_componentBody.Density = m_baseDensity + total;
		}

		public ReadOnlyList<int> GetClothes(ClothingSlot slot) => new ReadOnlyList<int>(m_clothes[slot]);

		private ClothingData GetClothingData(int value)
		{
			Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
			return block.GetClothingData(value);
		}

		private Color GetClothingColor(int value)
		{
			int data = Terrain.ExtractData(value);
			return SubsystemPalette.GetFabricColor(m_subsystemTerrain, new int?(ClothingBlock.GetClothingColor(data)));
		}

		public bool CanWearClothing(int value)
		{
			Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
			if (!(block is ClothingBlock))
				return false;
			ClothingData data = block.GetClothingData(value);
			return data != null;
		}

		public float ApplyArmorProtection(float attackPower)
		{
			if (attackPower <= 0f) return 0f;
			float num = m_random.Float(0f, 1f);
			ClothingSlot slot = (num < 0.1f) ? ClothingSlot.Feet :
							   (num < 0.3f) ? ClothingSlot.Legs :
							   (num < 0.9f) ? ClothingSlot.Torso : ClothingSlot.Head;

			List<int> clothes = new List<int>(GetClothes(slot));
			List<int> after = new List<int>(clothes);
			float remaining = attackPower;

			for (int i = 0; i < clothes.Count; i++)
			{
				int value = clothes[i];
				ClothingData data = GetClothingData(value);
				if (data == null) continue;
				Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
				float dur = (float)(block.GetDurability(value) + 1);
				float dmg = (float)block.GetDamage(value);
				float maxAbs = (dur - dmg) / dur * data.Sturdiness;
				float absorb = MathF.Min(remaining * MathUtils.Saturate(data.ArmorProtection / 1f), maxAbs);
				if (absorb > 0f)
				{
					remaining -= absorb;
					{
						float raw = absorb / data.Sturdiness * dur + 0.001f;
						int pts = (int)(MathF.Floor(raw) + (m_random.Bool(MathUtils.Remainder(raw, 1f)) ? 1 : 0));
						after[i] = BlocksManager.DamageItem(value, pts, Entity);
					}
					if (!string.IsNullOrEmpty(data.ImpactSoundsFolder))
						m_subsystemAudio.PlayRandomSound(data.ImpactSoundsFolder, 1f, m_random.Float(-0.3f, 0.3f), m_componentBody.Position, 4f, 0.15f);
					else
						m_subsystemSoundMaterials.PlayImpactSound(value, m_componentBody.Position, 1f);
				}
			}

			for (int j = after.Count - 1; j >= 0; j--)
				if (!BlocksManager.Blocks[Terrain.ExtractContents(after[j])].CanWear(after[j]))
				{
					after.RemoveAt(j);
					m_subsystemParticles.AddParticleSystem(new BlockDebrisParticleSystem(m_subsystemTerrain,
						m_componentBody.Position + m_componentBody.StanceBoxSize / 2f, 1f, 1f, Color.White, 0), false);
				}

			after.Sort((a, b) => (GetClothingData(a)?.Layer ?? 0) - (GetClothingData(b)?.Layer ?? 0));
			SetClothes(slot, after);
			return MathF.Max(remaining, 0f);
		}

		public int GetSlotValue(int slotIndex)
		{
			if (slotIndex < 0 || slotIndex >= 4) return 0;
			var list = m_clothes[m_slotsOrder[slotIndex]];
			return list.Count > 0 ? list[list.Count - 1] : 0;
		}
		public int GetSlotCount(int slotIndex)
		{
			if (slotIndex < 0 || slotIndex >= 4) return 0;
			return m_clothes[m_slotsOrder[slotIndex]].Count > 0 ? 1 : 0;
		}
		public int GetSlotCapacity(int slotIndex, int value) => 0;
		public int GetSlotProcessCapacity(int slotIndex, int value)
		{
			Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
			return (block.CanWear(value) && CanWearClothing(value)) ? 1 : 0;
		}
		public void AddSlotItems(int slotIndex, int value, int count) { }
		public void ProcessSlotItems(int slotIndex, int value, int count, int processCount, out int processedValue, out int processedCount)
		{
			processedValue = 0; processedCount = 0;
			if (slotIndex < 0 || slotIndex >= 4 || count <= 0 || processCount != 1) return;
			Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
			if (block.CanWear(value) && CanWearClothing(value))
			{
				ClothingData data = GetClothingData(value);
				if (data != null)
				{
					data.Mount?.Invoke(value, null);
					ClothingSlot slot = m_slotsOrder[slotIndex];
					List<int> newList = new List<int>(m_clothes[slot]);
					newList.Add(value);
					SetClothes(slot, newList);
					processedValue = value;
					processedCount = 1;
				}
			}
		}
		public int RemoveSlotItems(int slotIndex, int count)
		{
			if (slotIndex < 0 || slotIndex >= 4 || count != 1) return 0;
			ClothingSlot slot = m_slotsOrder[slotIndex];
			List<int> current = m_clothes[slot];
			if (current.Count == 0) return 0;
			int lastValue = current[current.Count - 1];
			ClothingData data = GetClothingData(lastValue);
			data?.Dismount?.Invoke(lastValue, null);
			List<int> newList = new List<int>(current);
			newList.RemoveAt(newList.Count - 1);
			SetClothes(slot, newList);
			return 1;
		}
		public void DropAllItems(Vector3 position)
		{
			Random rand = new Random();
			SubsystemPickables pickables = Project.FindSubsystem<SubsystemPickables>(true);
			for (int i = 0; i < 4; i++)
			{
				if (GetSlotCount(i) > 0)
				{
					int value = GetSlotValue(i);
					int removed = RemoveSlotItems(i, 1);
					Vector3 velocity = rand.Float(5f, 10f) * Vector3.Normalize(
						new Vector3(rand.Float(-1f, 1f), rand.Float(1f, 2f), rand.Float(-1f, 1f)));
					pickables.AddPickable(value, removed, position, new Vector3?(velocity), null, Entity);
				}
			}
		}

		public void Update(float dt) => UpdateRenderTargets();

		private void UpdateRenderTargets()
		{
			// Si no hay textura de piel cacheada, intentar obtenerla de nuevo
			if (m_cachedSkinTexture == null)
			{
				m_cachedSkinTexture = m_componentHumanModel.TextureOverride;
				if (m_cachedSkinTexture == null)
				{
					Model model = m_componentHumanModel.Model;
					if (model != null)
					{
						foreach (ModelMesh mesh in model.Meshes)
						{
							foreach (ModelMeshPart part in mesh.MeshParts)
							{
								if (!string.IsNullOrEmpty(part.TexturePath))
								{
									m_cachedSkinTexture = ContentManager.Get<Texture2D>(part.TexturePath);
									break;
								}
							}
							if (m_cachedSkinTexture != null) break;
						}
					}
				}
				// Si sigue siendo null, no podemos hacer nada
				if (m_cachedSkinTexture == null) return;
			}

			// Crear o reajustar render targets según tamaño de la piel
			if (m_innerClothedTexture == null || m_innerClothedTexture.Width != m_cachedSkinTexture.Width || m_innerClothedTexture.Height != m_cachedSkinTexture.Height)
			{
				Utilities.Dispose(ref m_innerClothedTexture);
				m_innerClothedTexture = new RenderTarget2D(m_cachedSkinTexture.Width, m_cachedSkinTexture.Height, 1, ColorFormat.Rgba8888, DepthFormat.None);
				m_componentHumanModel.TextureOverride = m_innerClothedTexture;
				m_clothedTexturesValid = false;
			}
			if (m_outerClothedTexture == null || m_outerClothedTexture.Width != m_cachedSkinTexture.Width || m_outerClothedTexture.Height != m_cachedSkinTexture.Height)
			{
				Utilities.Dispose(ref m_outerClothedTexture);
				m_outerClothedTexture = new RenderTarget2D(m_cachedSkinTexture.Width, m_cachedSkinTexture.Height, 1, ColorFormat.Rgba8888, DepthFormat.None);
				m_componentOuterClothingModel.TextureOverride = m_outerClothedTexture;
				m_clothedTexturesValid = false;
			}

			// Siempre asignar los render targets para que el modelo tenga textura
			m_componentHumanModel.TextureOverride = m_innerClothedTexture;
			m_componentOuterClothingModel.TextureOverride = m_outerClothedTexture;

			if (!m_clothedTexturesValid)
			{
				m_clothedTexturesValid = true;
				Rectangle oldScissor = Display.ScissorRectangle;
				RenderTarget2D oldTarget = Display.RenderTarget;
				try
				{
					// Dibujar capa interior (piel + ropa interior)
					Display.RenderTarget = m_innerClothedTexture;
					Display.Clear(new Vector4?(new Vector4(Color.Transparent)), null, null);
					int batchIndex = 0;

					// Capa base: piel
					TexturedBatch2D batch = m_primitivesRenderer.TexturedBatch(m_cachedSkinTexture, false, batchIndex++,
						DepthStencilState.None, null, BlendState.NonPremultiplied, SamplerState.PointClamp);
					batch.QueueQuad(Vector2.Zero, new Vector2(m_cachedSkinTexture.Width, m_cachedSkinTexture.Height), 0f, Vector2.Zero, Vector2.One, Color.White);

					// Capas de ropa interior
					ClothingSlot[] innerOrder = ComponentClothing.m_innerSlotsOrder?.Length > 0
						? ComponentClothing.m_innerSlotsOrder
						: new[] { ClothingSlot.Head, ClothingSlot.Torso, ClothingSlot.Legs, ClothingSlot.Feet };
					foreach (ClothingSlot slot in innerOrder)
					{
						if (!m_clothes.ContainsKey(slot)) continue;
						foreach (int value in m_clothes[slot])
						{
							ClothingData data = GetClothingData(value);
							if (data == null || data.IsOuter) continue;
							if (data.Texture == null && !string.IsNullOrEmpty(data._textureName))
								data.Texture = ContentManager.Get<Texture2D>(data._textureName);
							if (data.Texture != null)
							{
								Color color = GetClothingColor(value);
								batch = m_primitivesRenderer.TexturedBatch(data.Texture, false, batchIndex++,
									DepthStencilState.None, null, BlendState.NonPremultiplied, SamplerState.PointClamp);
								batch.QueueQuad(Vector2.Zero, new Vector2(m_cachedSkinTexture.Width, m_cachedSkinTexture.Height), 0f, Vector2.Zero, Vector2.One, color);
							}
						}
					}
					m_primitivesRenderer.Flush(true, int.MaxValue);

					// Dibujar capa exterior
					Display.RenderTarget = m_outerClothedTexture;
					Display.Clear(new Vector4?(new Vector4(Color.Transparent)), null, null);
					batchIndex = 0;

					ClothingSlot[] outerOrder = ComponentClothing.m_outerSlotsOrder?.Length > 0
						? ComponentClothing.m_outerSlotsOrder
						: new[] { ClothingSlot.Head, ClothingSlot.Torso, ClothingSlot.Legs, ClothingSlot.Feet };
					foreach (ClothingSlot slot in outerOrder)
					{
						if (!m_clothes.ContainsKey(slot)) continue;
						foreach (int value in m_clothes[slot])
						{
							ClothingData data = GetClothingData(value);
							if (data == null || !data.IsOuter) continue;
							if (data.Texture == null && !string.IsNullOrEmpty(data._textureName))
								data.Texture = ContentManager.Get<Texture2D>(data._textureName);
							if (data.Texture != null)
							{
								Color color = GetClothingColor(value);
								batch = m_primitivesRenderer.TexturedBatch(data.Texture, false, batchIndex++,
									DepthStencilState.None, null, BlendState.NonPremultiplied, SamplerState.PointClamp);
								batch.QueueQuad(Vector2.Zero, new Vector2(m_cachedSkinTexture.Width, m_cachedSkinTexture.Height), 0f, Vector2.Zero, Vector2.One, color);
							}
						}
					}
					m_primitivesRenderer.Flush(true, int.MaxValue);
				}
				catch (Exception ex)
				{
					Log.Error($"Error en UpdateRenderTargets de ComponentCreatureClothing: {ex.Message}");
					m_clothedTexturesValid = false;
					// En caso de error, no desasignamos TextureOverride para mantener el último estado válido
				}
				finally
				{
					Display.RenderTarget = oldTarget;
					Display.ScissorRectangle = oldScissor;
				}
			}
		}
	}
}
