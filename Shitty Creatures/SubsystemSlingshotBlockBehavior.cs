using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemSlingshotBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks
		{
			get
			{
				return new int[] { SlingshotBlock.Index };
			}
		}

		public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentPlayer componentPlayer)
		{
			int slotValue = inventory.GetSlotValue(slotIndex);
			if (Terrain.ExtractContents(slotValue) != SlingshotBlock.Index)
			{
				return false;
			}
			int data = Terrain.ExtractData(slotValue);
			SlingshotBlock.AmmoType current = SlingshotBlock.GetAmmoType(data);

			SlingshotBlock.AmmoType next = current;
			bool isCreative = this.m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative;

			// Ciclo de 4 estados. En no-creativos, salta las municiones que no tengas en el inventario.
			for (int i = 0; i < 4; i++)
			{
				next = (SlingshotBlock.AmmoType)(((int)next + 1) % 4);
				if (next == SlingshotBlock.AmmoType.Empty)
				{
					break;
				}
				if (isCreative || this.HasAmmoInInventory(inventory, next))
				{
					break;
				}
			}

			int newData = SlingshotBlock.SetAmmoType(data, next);
			int newSlotValue = Terrain.MakeBlockValue(SlingshotBlock.Index, 0, newData);

			inventory.RemoveSlotItems(slotIndex, 1);
			inventory.AddSlotItems(slotIndex, newSlotValue, 1);

			if (componentPlayer != null)
			{
				// 1er mensaje: cuando pasa a estado vacio
				if (next == SlingshotBlock.AmmoType.Empty)
				{
					componentPlayer.ComponentGui.DisplaySmallMessage(LanguageControl.Get(fName, 0), Color.White, true, false);
				}
				else
				{
					// 2do mensaje: tiene cualquiera de las municiones y se muestra el nombre del bloque/municion
					int ammoValue = this.GetAmmoBlockValue(next);
					string name = BlocksManager.Blocks[Terrain.ExtractContents(ammoValue)].GetDisplayName(null, ammoValue);
					componentPlayer.ComponentGui.DisplaySmallMessage(name, Color.White, true, false);
				}
			}
			return true;
		}

		private bool HasAmmoInInventory(IInventory inventory, SlingshotBlock.AmmoType type)
		{
			if (type == SlingshotBlock.AmmoType.Empty) return true;
			int ammoValue = this.GetAmmoBlockValue(type);
			int contents = Terrain.ExtractContents(ammoValue);
			return this.FindInventorySlotWithContents(inventory, contents) >= 0;
		}

		public override int GetProcessInventoryItemCapacity(IInventory inventory, int slotIndex, int value)
		{
			return 0;
		}

		public override void ProcessInventoryItem(IInventory inventory, int slotIndex, int value, int count, int processCount, out int processedValue, out int processedCount)
		{
			processedValue = value;
			processedCount = count;
		}

		public override bool OnAim(Ray3 aim, ComponentMiner componentMiner, AimState state)
		{
			IInventory inventory = componentMiner.Inventory;
			if (inventory == null)
			{
				return false;
			}
			int activeSlotIndex = inventory.ActiveSlotIndex;
			if (activeSlotIndex < 0)
			{
				return false;
			}
			int slotValue = inventory.GetSlotValue(activeSlotIndex);
			int slotCount = inventory.GetSlotCount(activeSlotIndex);
			int contents = Terrain.ExtractContents(slotValue);
			if (slotCount <= 0 || contents != SlingshotBlock.Index)
			{
				return false;
			}
			int data = Terrain.ExtractData(slotValue);
			SlingshotBlock.AmmoType ammoType = SlingshotBlock.GetAmmoType(data);

			double gameTime;
			if (!this.m_aimStartTimes.TryGetValue(componentMiner, out gameTime))
			{
				gameTime = this.m_subsystemTime.GameTime;
				this.m_aimStartTimes[componentMiner] = gameTime;
			}
			float elapsed = (float)(this.m_subsystemTime.GameTime - gameTime);
			float t = (float)MathUtils.Remainder(this.m_subsystemTime.GameTime, 1000.0);

			// El temblor de mano aumenta más rápido por la tensión de la goma elástica
			Vector3 shake = ((componentMiner.ComponentCreature.ComponentBody.IsCrouching ? 0.03f : 0.05f) + 0.3f * MathUtils.Saturate((elapsed - 1.5f) / 4f)) * new Vector3
			{
				X = SimplexNoise.OctavedNoise(t, 2f, 3, 2f, 0.5f, false),
				Y = SimplexNoise.OctavedNoise(t + 100f, 2f, 3, 2f, 0.5f, false),
				Z = SimplexNoise.OctavedNoise(t + 200f, 2f, 3, 2f, 0.5f, false)
			};
			aim.Direction = Vector3.Normalize(aim.Direction + shake);

			switch (state)
			{
				case AimState.InProgress:
					{
						// Cansancio rápido por sostener la goma elástica (6s en lugar de 9s)
						if (elapsed >= 6f)
						{
							componentMiner.ComponentCreature.ComponentCreatureSounds.PlayMoanSound();
							return true;
						}
						ComponentFirstPersonModel firstPerson = componentMiner.Entity.FindComponent<ComponentFirstPersonModel>();
						if (firstPerson != null)
						{
							ComponentPlayer player = componentMiner.ComponentPlayer;
							if (player != null)
							{
								player.ComponentAimingSights.ShowAimingSights(aim.Position, aim.Direction);
							}
							firstPerson.ItemOffsetOrder = new Vector3(-0.1f, 0.15f, 0f);
							firstPerson.ItemRotationOrder = new Vector3(0f, -0.7f, 0f);
						}
						componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 1.2f;
						componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = new Vector3(0f, 0f, 0f);
						componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = new Vector3(0f, -0.2f, 0f);

						// Tensado más rápido: alcanza el máximo en ~1 segundo (0.06s por nivel en lugar de 0.1s)
						if (this.m_subsystemTime.PeriodicGameTimeEvent(0.06f, 0.0))
						{
							int draw = MathUtils.Min(SlingshotBlock.GetDraw(data) + 1, 15);
							inventory.RemoveSlotItems(activeSlotIndex, 1);
							inventory.AddSlotItems(activeSlotIndex, Terrain.MakeBlockValue(contents, 0, SlingshotBlock.SetDraw(data, draw)), 1);
						}
						break;
					}
				case AimState.Cancelled:
					{
						inventory.RemoveSlotItems(activeSlotIndex, 1);
						inventory.AddSlotItems(activeSlotIndex, Terrain.MakeBlockValue(contents, 0, SlingshotBlock.SetDraw(data, 0)), 1);
						this.m_aimStartTimes.Remove(componentMiner);
						break;
					}
				case AimState.Completed:
					{
						int draw2 = SlingshotBlock.GetDraw(data);

						// Si está vacío, mostrar mensaje (solo jugadores) y cancelar
						if (ammoType == SlingshotBlock.AmmoType.Empty)
						{
							ComponentPlayer p = componentMiner.ComponentPlayer;
							if (p != null)
							{
								p.ComponentGui.DisplaySmallMessage(LanguageControl.Get(fName, 1), Color.White, true, false);
							}
						}
						else
						{
							int ammoValue = this.GetAmmoBlockValue(ammoType);
							Vector3 origin = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition
										   + componentMiner.ComponentCreature.ComponentBody.Matrix.Right * 0.3f
										   - componentMiner.ComponentCreature.ComponentBody.Matrix.Up * 0.2f;
							Vector3 dir = Vector3.Normalize(origin + aim.Direction * 10f - origin);

							// Física de liga de resortera: dispara con velocidad base desde el inicio y un máximo de 32
							float speed = MathUtils.Lerp(8f, 32f, MathF.Pow((float)draw2 / 15f, 1.15f));

							bool isPlayer = componentMiner.ComponentPlayer != null;

							if (isPlayer)
							{
								// Multiplicador de fuerza solo para jugadores
								speed *= 0.5f * (componentMiner.ComponentPlayer.ComponentLevel.StrengthFactor - 1f) + 1f;

								// Verificar si el jugador tiene la munición en su inventario
								int ammoSlot = this.FindInventorySlotWithContents(inventory, Terrain.ExtractContents(ammoValue));
								if (ammoSlot < 0)
								{
									componentMiner.ComponentPlayer.ComponentGui.DisplaySmallMessage(LanguageControl.Get(fName, 1), Color.White, true, false);

									// Reset de tension del slingshot y salir sin disparar
									inventory.RemoveSlotItems(activeSlotIndex, 1);
									inventory.AddSlotItems(activeSlotIndex, Terrain.MakeBlockValue(contents, 0, SlingshotBlock.SetDraw(data, 0)), 1);
									this.m_aimStartTimes.Remove(componentMiner);
									return false;
								}

								Vector3 velocity = componentMiner.ComponentCreature.ComponentBody.Velocity + dir * speed;

								if (this.m_subsystemProjectiles.FireProjectile(ammoValue, origin, velocity, Vector3.Zero, componentMiner.ComponentCreature) != null)
								{
									// Consumir 1 del slot que tenia la municion
									inventory.RemoveSlotItems(ammoSlot, 1);

									// Si nos quedamos sin municion de ese tipo, bajar a vacio automaticamente en no-creativos
									if (inventory.GetSlotCount(ammoSlot) == 0 && this.m_subsystemGameInfo.WorldSettings.GameMode != GameMode.Creative)
									{
										data = SlingshotBlock.SetAmmoType(data, SlingshotBlock.AmmoType.Empty);
									}
									this.m_subsystemAudio.PlaySound("Audio/Bow", 1f, this.m_random.Float(-0.1f, 0.1f), origin, 3f, true);
								}
							}
							else
							{
								// Comportamiento de IA: No verifica inventario, solo dispara el proyectil
								Vector3 velocity = componentMiner.ComponentCreature.ComponentBody.Velocity + dir * speed;

								if (this.m_subsystemProjectiles.FireProjectile(ammoValue, origin, velocity, Vector3.Zero, componentMiner.ComponentCreature) != null)
								{
									this.m_subsystemAudio.PlaySound("Audio/Bow", 1f, this.m_random.Float(-0.1f, 0.1f), origin, 3f, true);
								}
							}
						}

						// Reset de tension del slingshot
						inventory.RemoveSlotItems(activeSlotIndex, 1);
						inventory.AddSlotItems(activeSlotIndex, Terrain.MakeBlockValue(contents, 0, SlingshotBlock.SetDraw(data, 0)), 1);

						// Dano al slingshot por uso
						int damageCount = 0;
						if (draw2 >= 15) damageCount = 2;
						else if (draw2 >= 4) damageCount = 1;
						componentMiner.DamageActiveTool(damageCount);

						this.m_aimStartTimes.Remove(componentMiner);
						break;
					}
			}
			return false;
		}

		private int FindInventorySlotWithContents(IInventory inventory, int contents)
		{
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				if (inventory.GetSlotCount(i) > 0 && Terrain.ExtractContents(inventory.GetSlotValue(i)) == contents)
				{
					return i;
				}
			}
			return -1;
		}

		private int GetAmmoBlockValue(SlingshotBlock.AmmoType type)
		{
			switch (type)
			{
				case SlingshotBlock.AmmoType.Stone:
					return Terrain.MakeBlockValue(StoneChunkBlock.Index);
				case SlingshotBlock.AmmoType.Snowball:
					return Terrain.MakeBlockValue(SnowballBlock.Index);
				case SlingshotBlock.AmmoType.BuckshotBall:
					return Terrain.MakeBlockValue(BulletBlock.Index, 0, BulletBlock.SetBulletType(0, BulletBlock.BulletType.BuckshotBall));
				default:
					return 0;
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			base.Load(valuesDictionary);
		}

		public SubsystemTime m_subsystemTime;
		public SubsystemProjectiles m_subsystemProjectiles;
		public SubsystemAudio m_subsystemAudio;
		public SubsystemGameInfo m_subsystemGameInfo;
		public Random m_random = new Random();
		public Dictionary<ComponentMiner, double> m_aimStartTimes = new Dictionary<ComponentMiner, double>();

		public static string fName = "SubsystemSlingshotBlockBehavior";
	}
}
