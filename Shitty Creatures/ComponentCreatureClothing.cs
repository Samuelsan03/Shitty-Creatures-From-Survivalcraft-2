using System;
using System.Collections.Generic;
using System.Linq;
using Engine;
using Engine.Graphics;
using Engine.Serialization;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	/// Componente que permite a una criatura (no jugador) usar ropa del juego.
	/// Se apoya en ComponentHumanModel para el renderizado y usa las mismas
	/// texturas de ropa que los jugadores.
	/// </summary>
	public class ComponentCreatureClothing : Component, IUpdateable, IInventory
	{
		// ---------- Dependencias del subsistema ----------
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemModelsRenderer m_subsystemModelsRenderer;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemTime m_subsystemTime;
		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemPickables m_subsystemPickables;
		private SubsystemBlockBehaviors m_subsystemBlockBehaviors;
		private SubsystemSoundMaterials m_subsystemSoundMaterials;

		// ---------- Componentes de la entidad ----------
		private ComponentHumanModel m_componentHumanModel;
		private ComponentOuterClothingModel m_componentOuterClothingModel;
		private ComponentBody m_componentBody;
		private ComponentCreature m_componentCreature;

		// ---------- Texturas y render targets ----------
		private Texture2D m_baseSkinTexture;            // Textura original del modelo (base para la ropa)
		private string m_baseSkinTextureName;           // Solo para seguimiento (opcional)
		private RenderTarget2D m_innerClothedTexture;
		private RenderTarget2D m_outerClothedTexture;
		private bool m_clothedTexturesValid;
		private PrimitivesRenderer2D m_primitivesRenderer = new PrimitivesRenderer2D();
		private Random m_random = new Random();

		// ---------- Datos de la ropa ----------
		private Dictionary<ClothingSlot, List<int>> m_clothes = new Dictionary<ClothingSlot, List<int>>();
		private List<int> m_clothesList = new List<int>();

		// ---------- Propiedades de aislamiento (insulation) ----------
		public float Insulation { get; set; }
		public ClothingSlot LeastInsulatedSlot { get; set; }
		public float SteedMovementSpeedFactor { get; set; } = 1f;
		public float DensityModifierApplied { get; set; } = 0f;
		public Dictionary<ClothingSlot, float> InsulationBySlots { get; } = new Dictionary<ClothingSlot, float>();

		// ---------- IInventory ----------
		public Project Project => base.Project;
		public int SlotsCount => ClothingSlot.ClothingSlots.Count;
		public int VisibleSlotsCount { get; set; }
		public int ActiveSlotIndex { get; set; } = -1;

		// ---------- IUpdateable ----------
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		// ---------- Inicialización y carga ----------
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// Subsistemas
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemModelsRenderer = Project.FindSubsystem<SubsystemModelsRenderer>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true);
			m_subsystemBlockBehaviors = Project.FindSubsystem<SubsystemBlockBehaviors>(true);
			m_subsystemSoundMaterials = Project.FindSubsystem<SubsystemSoundMaterials>(true);

			// Componentes de la entidad
			m_componentHumanModel = Entity.FindComponent<ComponentHumanModel>(true);
			m_componentOuterClothingModel = Entity.FindComponent<ComponentOuterClothingModel>(false);
			m_componentBody = Entity.FindComponent<ComponentBody>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);

			// Inicializar diccionario de slots
			foreach (var slot in ClothingSlot.ClothingSlots.Values)
			{
				m_clothes[slot] = new List<int>();
				InsulationBySlots[slot] = slot.BasicInsulation;
			}

			// Cargar la ropa desde el XML (parámetro "CreatureClothes")
			var clothesNode = valuesDictionary.GetValue<ValuesDictionary>("CreatureClothes");
			foreach (var slotName in ClothingSlot.ClothingSlots.Keys)
			{
				var slot = ClothingSlot.ClothingSlots[slotName];
				var listStr = clothesNode.GetValue<string>(slotName, "");
				var list = HumanReadableConverter.ValuesListFromString<int>(';', listStr);
				SetClothes(slot, list);
			}

			// ---------- OBTENER LA TEXTURA BASE ----------
			// 1. Si el modelo ya tiene TextureOverride, usarlo como base.
			// 2. Si no, intentar cargar desde el parámetro SkinTextureName (ruta de textura, ej: "Textures/MiCriatura")
			// 3. Si todo falla, usar una textura blanca por defecto.
			string skinName = valuesDictionary.GetValue<string>("SkinTextureName", null);

			if (m_componentHumanModel.TextureOverride != null)
			{
				m_baseSkinTexture = m_componentHumanModel.TextureOverride;
				m_baseSkinTextureName = "FromTextureOverride";
			}
			else if (!string.IsNullOrEmpty(skinName))
			{
				try
				{
					m_baseSkinTexture = ContentManager.Get<Texture2D>(skinName);
					m_baseSkinTextureName = skinName;
				}
				catch (Exception ex)
				{
					Log.Warning($"ComponentCreatureClothing: Could not load texture '{skinName}'. Using fallback. Error: {ex.Message}");
					m_baseSkinTexture = null;
				}
			}

			// Fallback: textura blanca de 1x1
			if (m_baseSkinTexture == null)
			{
				m_baseSkinTexture = Texture2D.Load(Color.White, 1, 1);
				m_baseSkinTextureName = "FallbackWhite";
			}

			// Suscribirse al evento de reset de dispositivo para regenerar texturas
			Display.DeviceReset += Display_DeviceReset;
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			base.Save(valuesDictionary, entityToIdMap);
			var clothesNode = new ValuesDictionary();
			foreach (var slotName in ClothingSlot.ClothingSlots.Keys)
			{
				var slot = ClothingSlot.ClothingSlots[slotName];
				clothesNode.SetValue<string>(slotName, HumanReadableConverter.ValuesListToString(';', m_clothes[slot].ToArray()));
			}
			valuesDictionary.SetValue("CreatureClothes", clothesNode);
		}

		public override void Dispose()
		{
			base.Dispose();
			// No liberamos m_baseSkinTexture si es del ContentManager o del modelo.
			// Solo liberamos si la creamos nosotros (fallback).
			if (m_baseSkinTexture != null && m_baseSkinTextureName == "FallbackWhite" && !ContentManager.IsContent(m_baseSkinTexture))
				m_baseSkinTexture.Dispose();
			m_innerClothedTexture?.Dispose();
			m_outerClothedTexture?.Dispose();
			Display.DeviceReset -= Display_DeviceReset;
		}

		private void Display_DeviceReset()
		{
			m_clothedTexturesValid = false;
		}

		// ---------- Gestión de la ropa ----------
		public ReadOnlyList<int> GetClothes(ClothingSlot slot)
		{
			return new ReadOnlyList<int>(m_clothes[slot]);
		}

		public void SetClothes(ClothingSlot slot, IEnumerable<int> clothes)
		{
			var list = clothes as List<int> ?? clothes.ToList();
			if (!m_clothes[slot].SequenceEqual(list))
			{
				m_clothes[slot].Clear();
				m_clothes[slot].AddRange(list);
				m_clothedTexturesValid = false;

				// Recalcular estadísticas
				DensityModifierApplied = 0f;
				SteedMovementSpeedFactor = 1f;
				foreach (var s in ClothingSlot.ClothingSlots.Values)
					InsulationBySlots[s] = s.BasicInsulation;

				foreach (var kv in m_clothes)
				{
					foreach (int value in kv.Value)
					{
						var data = BlocksManager.Blocks[Terrain.ExtractContents(value)].GetClothingData(value);
						if (data != null)
						{
							DensityModifierApplied += data.DensityModifier;
							SteedMovementSpeedFactor *= data.SteedMovementSpeedFactor;
							InsulationBySlots[data.Slot] += data.Insulation;
						}
					}
				}

				// Actualizar densidad (si ComponentBody existe)
				if (m_componentBody != null)
					m_componentBody.Density += DensityModifierApplied;

				CalculateInsulationFromSlots();
			}
		}

		public float CalculateInsulationFromSlots()
		{
			float sum = 0f;
			float min = float.MaxValue;
			ClothingSlot least = ClothingSlot.Feet;
			foreach (var slot in ClothingSlot.ClothingSlots.Values)
			{
				float val = InsulationBySlots[slot];
				sum += 1f / val;
				if (val < min) { min = val; least = slot; }
			}
			Insulation = 1f / sum;
			LeastInsulatedSlot = least;
			return Insulation;
		}

		// ---------- Actualización ----------
		public void Update(float dt)
		{
			// Actualizar render targets si es necesario
			UpdateRenderTargets();

			// Degradado de ropa con el tiempo (solo eliminar prendas rotas)
			if (m_subsystemTime.PeriodicGameTimeEvent(2.0, 0.0))
			{
				foreach (var slot in ClothingSlot.ClothingSlots.Values)
				{
					bool changed = false;
					m_clothesList.Clear();
					m_clothesList.AddRange(m_clothes[slot]);
					for (int i = 0; i < m_clothesList.Count; i++)
					{
						int val = m_clothesList[i];
						int damage = BlocksManager.Blocks[Terrain.ExtractContents(val)].GetDamage(val);
						int durability = BlocksManager.Blocks[Terrain.ExtractContents(val)].GetDurability(val);
						if (durability >= 0 && damage >= durability)
						{
							m_clothesList[i] = 0;
							changed = true;
						}
					}
					m_clothesList.RemoveAll(v => v == 0 || !BlocksManager.Blocks[Terrain.ExtractContents(v)].CanWear(v));
					if (changed)
						SetClothes(slot, m_clothesList);
				}
			}
		}

		// ---------- Renderizado de texturas combinadas ----------
		private void UpdateRenderTargets()
		{
			// Si no hay textura base, no podemos hacer nada
			if (m_baseSkinTexture == null)
				return;

			// Crear render targets si no existen o cambiaron de tamaño
			if (m_innerClothedTexture == null || m_innerClothedTexture.Width != m_baseSkinTexture.Width || m_innerClothedTexture.Height != m_baseSkinTexture.Height)
			{
				m_innerClothedTexture = new RenderTarget2D(m_baseSkinTexture.Width, m_baseSkinTexture.Height, 1, ColorFormat.Rgba8888, DepthFormat.None);
				// Asignar el render target combinado al modelo
				m_componentHumanModel.TextureOverride = m_innerClothedTexture;
				m_clothedTexturesValid = false;
			}
			if (m_outerClothedTexture == null && m_componentOuterClothingModel != null)
			{
				m_outerClothedTexture = new RenderTarget2D(m_baseSkinTexture.Width, m_baseSkinTexture.Height, 1, ColorFormat.Rgba8888, DepthFormat.None);
				m_componentOuterClothingModel.TextureOverride = m_outerClothedTexture;
				m_clothedTexturesValid = false;
			}

			// Regenerar texturas si es necesario
			if (!m_clothedTexturesValid)
			{
				m_clothedTexturesValid = true;
				var scissor = Display.ScissorRectangle;
				var oldRT = Display.RenderTarget;

				try
				{
					// ----- Renderizar textura interior (piel + ropa interior) -----
					Display.RenderTarget = m_innerClothedTexture;
					Display.Clear(new Vector4?(new Vector4(Color.Transparent)), null, null);
					int layer = 0;

					// Dibujar la piel de fondo (USANDO LA TEXTURA BASE DEL MODELO)
					var batch = m_primitivesRenderer.TexturedBatch(m_baseSkinTexture, false, layer++, DepthStencilState.None, null, BlendState.NonPremultiplied, SamplerState.PointClamp);
					batch.QueueQuad(Vector2.Zero, new Vector2(m_innerClothedTexture.Width, m_innerClothedTexture.Height), 0f, Vector2.Zero, Vector2.One, Color.White);

					// Dibujar ropa interior (no outer)
					var innerSlots = new ClothingSlot[] { ClothingSlot.Head, ClothingSlot.Torso, ClothingSlot.Legs, ClothingSlot.Feet };
					foreach (var slot in innerSlots)
					{
						foreach (int value in m_clothes[slot])
						{
							var data = BlocksManager.Blocks[Terrain.ExtractContents(value)].GetClothingData(value);
							if (data != null && !data.IsOuter)
							{
								if (data.Texture == null)
									data.Texture = ContentManager.Get<Texture2D>(data._textureName);

								int clothingColor = ClothingBlock.GetClothingColor(Terrain.ExtractData(value));
								Color color = SubsystemPalette.GetFabricColor(m_subsystemTerrain, new int?(clothingColor));

								batch = m_primitivesRenderer.TexturedBatch(data.Texture, false, layer++, DepthStencilState.None, null, BlendState.NonPremultiplied, SamplerState.PointClamp);
								batch.QueueQuad(Vector2.Zero, new Vector2(m_innerClothedTexture.Width, m_innerClothedTexture.Height), 0f, Vector2.Zero, Vector2.One, color);
							}
						}
					}
					m_primitivesRenderer.Flush(true, int.MaxValue);

					// ----- Renderizar textura exterior (ropa outer) -----
					if (m_outerClothedTexture != null)
					{
						Display.RenderTarget = m_outerClothedTexture;
						Display.Clear(new Vector4?(new Vector4(Color.Transparent)), null, null);
						layer = 0;
						var outerSlots = new ClothingSlot[] { ClothingSlot.Head, ClothingSlot.Torso, ClothingSlot.Legs, ClothingSlot.Feet };
						foreach (var slot in outerSlots)
						{
							foreach (int value in m_clothes[slot])
							{
								var data = BlocksManager.Blocks[Terrain.ExtractContents(value)].GetClothingData(value);
								if (data != null && data.IsOuter)
								{
									if (data.Texture == null)
										data.Texture = ContentManager.Get<Texture2D>(data._textureName);

									int clothingColor = ClothingBlock.GetClothingColor(Terrain.ExtractData(value));
									Color color = SubsystemPalette.GetFabricColor(m_subsystemTerrain, new int?(clothingColor));

									batch = m_primitivesRenderer.TexturedBatch(data.Texture, false, layer++, DepthStencilState.None, null, BlendState.NonPremultiplied, SamplerState.PointClamp);
									batch.QueueQuad(Vector2.Zero, new Vector2(m_outerClothedTexture.Width, m_outerClothedTexture.Height), 0f, Vector2.Zero, Vector2.One, color);
								}
							}
						}
						m_primitivesRenderer.Flush(true, int.MaxValue);
					}
				}
				finally
				{
					Display.RenderTarget = oldRT;
					Display.ScissorRectangle = scissor;
				}
			}
		}

		// ---------- IInventory ----------
		public int GetSlotValue(int slotIndex)
		{
			var slot = (ClothingSlot)slotIndex;
			return m_clothes[slot].LastOrDefault();
		}

		public int GetSlotCount(int slotIndex)
		{
			var slot = (ClothingSlot)slotIndex;
			return m_clothes[slot].Count;
		}

		public int GetSlotCapacity(int slotIndex, int value)
		{
			return 0;
		}

		public int GetSlotProcessCapacity(int slotIndex, int value)
		{
			var block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
			if (block.GetNutritionalValue(value) > 0f)
				return 1;
			if (block.CanWear(value) && CanWearClothing(value))
				return 1;
			return 0;
		}

		public void AddSlotItems(int slotIndex, int value, int count)
		{
		}

		public void ProcessSlotItems(int slotIndex, int value, int count, int processCount, out int processedValue, out int processedCount)
		{
			processedValue = 0;
			processedCount = 0;

			if (processCount != 1) return;

			var block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
			if (block.GetNutritionalValue(value) > 0f)
			{
				return;
			}

			if (block.CanWear(value))
			{
				var data = block.GetClothingData(value);
				if (data == null) return;

				var slot = (ClothingSlot)slotIndex;
				var clothes = new List<int>(m_clothes[slot]) { value };
				SetClothes(slot, clothes);
				processedValue = value;
				processedCount = 1;
			}
		}

		public int RemoveSlotItems(int slotIndex, int count)
		{
			if (count == 1)
			{
				var slot = (ClothingSlot)slotIndex;
				var list = new List<int>(m_clothes[slot]);
				if (list.Count > 0)
				{
					list.RemoveAt(list.Count - 1);
					SetClothes(slot, list);
					return 1;
				}
			}
			return 0;
		}

		public void DropAllItems(Vector3 position)
		{
			foreach (var slot in ClothingSlot.ClothingSlots.Values)
			{
				foreach (int value in m_clothes[slot])
				{
					m_subsystemPickables.AddPickable(value, 1, position, null, null);
				}
				m_clothes[slot].Clear();
			}
			m_clothedTexturesValid = false;
		}

		public bool CanWearClothing(int value)
		{
			var data = BlocksManager.Blocks[Terrain.ExtractContents(value)].GetClothingData(value);
			if (data == null) return false;
			var list = m_clothes[data.Slot];
			if (list.Count == 0) return true;
			int last = list[list.Count - 1];
			var lastData = BlocksManager.Blocks[Terrain.ExtractContents(last)].GetClothingData(last);
			return lastData != null && data.Layer > lastData.Layer;
		}

		public float ApplyArmorProtection(Attackment attackment)
		{
			if (attackment.AttackPower <= 0f)
				return attackment.AttackPower;

			float roll = m_random.Float(0f, 1f);
			ClothingSlot slot;
			if (roll < 0.1f) slot = ClothingSlot.Feet;
			else if (roll < 0.3f) slot = ClothingSlot.Legs;
			else if (roll < 0.9f) slot = ClothingSlot.Torso;
			else slot = ClothingSlot.Head;

			List<int> before = new List<int>(m_clothes[slot]);
			List<int> after = new List<int>(before);

			float remainingPower = attackment.AttackPower;

			// ===== DIVISOR FIJO PARA PROTECCIÓN =====
			float armorDivision = 10f;
			// =========================================

			for (int i = 0; i < before.Count; i++)
			{
				int val = before[i];
				var data = BlocksManager.Blocks[Terrain.ExtractContents(val)].GetClothingData(val);
				if (data != null)
				{
					float armor = data.ArmorProtection;
					float sturdiness = data.Sturdiness;

					float damageAbsorbed = MathF.Min(
						remainingPower * MathUtils.Saturate(armor / armorDivision),
						sturdiness
					);

					if (damageAbsorbed > 0f)
					{
						remainingPower -= damageAbsorbed;

						// ===== CÁLCULO DE DESGASTE =====
						int currentValue = after[i];
						int durability = BlocksManager.Blocks[Terrain.ExtractContents(currentValue)].GetDurability(currentValue);
						float x = damageAbsorbed / sturdiness * (durability + 1) + 0.001f;
						int damageToAdd = (int)MathF.Floor(x);
						if (m_random.Bool(MathUtils.Remainder(x, 1f)))
							damageToAdd++;
						// ================================

						int newDamage = BlocksManager.Blocks[Terrain.ExtractContents(currentValue)].GetDamage(currentValue) + damageToAdd;

						if (newDamage > durability)
						{
							after[i] = 0;
							m_subsystemParticles.AddParticleSystem(
								new BlockDebrisParticleSystem(m_subsystemTerrain,
									m_componentBody.Position + m_componentBody.StanceBoxSize / 2f,
									1f, 1f, Color.White, 0), false);

							// ===== SONIDO DE IMPACTO =====
							// Usar SubsystemSoundMaterials si está disponible
							if (m_subsystemSoundMaterials != null)
							{
								// Si es un proyectil, usar su valor para el sonido de impacto
								if (attackment is ProjectileAttackment projectileAttack && projectileAttack.Projectile != null)
								{
									m_subsystemSoundMaterials.PlayImpactSound(projectileAttack.Projectile.Value, m_componentBody.Position, 1f);
								}
								else if (!string.IsNullOrEmpty(data.ImpactSoundsFolder))
								{
									// Si no es proyectil, usar el sonido de la carpeta de la ropa
									m_subsystemAudio.PlayRandomSound(data.ImpactSoundsFolder, 1f,
										m_random.Float(-0.3f, 0.3f),
										m_componentBody.Position, 4f, 0.15f);
								}
							}
							else if (!string.IsNullOrEmpty(data.ImpactSoundsFolder))
							{
								// Fallback si no hay SubsystemSoundMaterials
								m_subsystemAudio.PlayRandomSound(data.ImpactSoundsFolder, 1f,
									m_random.Float(-0.3f, 0.3f),
									m_componentBody.Position, 4f, 0.15f);
							}
							// ============================
						}
						else
						{
							after[i] = BlocksManager.Blocks[Terrain.ExtractContents(currentValue)]
								.SetDamage(currentValue, newDamage);
						}
					}
				}
			}

			after.RemoveAll(v => v == 0 || !BlocksManager.Blocks[Terrain.ExtractContents(v)].CanWear(v));
			SetClothes(slot, after);

			attackment.AttackPower = Math.Max(remainingPower, 0f);
			return attackment.AttackPower;
		}

		// ---------- Método estático para obtener índice de slot ----------
		public static int GetClothingSlotIndex(ClothingSlot slot)
		{
			var slots = ClothingSlot.ClothingSlots.Values.ToList();
			return slots.IndexOf(slot);
		}
	}
}
