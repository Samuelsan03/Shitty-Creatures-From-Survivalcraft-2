using System;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Añadimos IPlayerNoiseListener a la definición de la clase
	public class ComponentShittyChristmasBehavior : ComponentBehavior, IUpdateable, IPlayerNoiseListener
	{
		public const string fName = "ComponentShittyChristmasBehavior";

		public override float ImportanceLevel
		{
			get
			{
				return 1f;
			}
		}

		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			this.m_subsystemSky = base.Project.FindSubsystem<SubsystemSky>(true);
			this.m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			this.m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			this.m_subsystemPickables = base.Project.FindSubsystem<SubsystemPickables>(true);
			this.m_componentGui = base.Entity.FindComponent<ComponentGui>(true);
			this.m_componentPlayer = base.Entity.FindComponent<ComponentPlayer>(true);
			this.m_componentSleep = base.Entity.FindComponent<ComponentSleep>();

			// Cacheamos el índice del bloque del huevo usando BlocksManager.GetBlock("")
			Block eggBlock = BlocksManager.GetBlock("ChristmasGiftEggBlock");
			this.m_eggBlockIndex = (eggBlock != null) ? eggBlock.BlockIndex : -1;
		}

		public void Update(float dt)
		{
			if (this.m_componentGui == null || this.m_componentPlayer == null)
			{
				return;
			}

			bool isSleeping = (this.m_componentSleep != null && this.m_componentSleep.IsSleeping);

			if (Time.PeriodicEvent(5.0, 0.0) && !isSleeping)
			{
				DateTime now = DateTime.Now;
				bool isChristmas = (now.Month == 12 && now.Day == 25);
				bool isNight = (this.m_subsystemSky.SkyLightIntensity == 0f);

				if (isChristmas && isNight)
				{
					if (!this.m_celebrationActive)
					{
						this.m_celebrationActive = true;

						if (now.Year != ComponentShittyChristmasBehavior.m_lastMessageYear)
						{
							ComponentShittyChristmasBehavior.m_lastMessageYear = now.Year;
							this.m_componentGui.DisplayLargeMessage(LanguageControl.Get(fName, 1), LanguageControl.Get(fName, 2), 15f, 3f);
						}

						if (InGameMusicManager.CurrentTrack != "MenuMusic/Jingle Bells")
						{
							InGameMusicManager.PlayMusic("MenuMusic/Jingle Bells", 0f, InGameMusicManager.MusicContext.InGame, true);
						}
					}
				}
				else
				{
					if (this.m_celebrationActive)
					{
						this.m_celebrationActive = false;

						if (InGameMusicManager.CurrentTrack == "MenuMusic/Jingle Bells")
						{
							InGameMusicManager.FadeOutAndStop(3.0);
						}
					}
				}
			}

			// Lógica de fuegos artificiales automáticos
			if (this.m_celebrationActive && !isSleeping)
			{
				float num = MathUtils.Lerp(1f, 7f, 0.5f * MathF.Sin(0.25f * (float)Time.FrameStartTime) + 0.5f);
				if (this.m_random.Float(0f, 1f) < num * dt)
				{
					Vector2 vector = this.m_random.Vector2(35f, 50f);
					Vector3 vector2 = this.m_componentPlayer.ComponentBody.Position + new Vector3(vector.X, 0f, vector.Y);

					TerrainRaycastResult? terrainRaycastResult = this.m_subsystemTerrain.Raycast(new Vector3(vector2.X, 120f, vector2.Z), new Vector3(vector2.X, 40f, vector2.Z), false, true, null);
					if (terrainRaycastResult != null)
					{
						TerrainRaycastResult value2 = terrainRaycastResult.Value;
						float x = (float)value2.CellFace.Point.X;
						value2 = terrainRaycastResult.Value;
						float y = (float)(value2.CellFace.Point.Y + 1);
						value2 = terrainRaycastResult.Value;
						Vector3 position = new Vector3(x, y, (float)value2.CellFace.Point.Z);

						this.SpawnFirework(position);
					}
				}
			}

			// === LLUVIA DE HUEVOS ===
			// Imítamos el comportamiento de la lluvia: huevos cayendo del cielo alrededor del jugador
			if (this.m_celebrationActive && !isSleeping && this.m_eggBlockIndex >= 0)
			{
				this.UpdateEggRain(dt);
			}
		}

		/// <summary>
		/// Lluvia de huevos: imita la precipitación de SubsystemWeather.
		/// Spawnea huevos cayendo del cielo en un radio alrededor del jugador,
		/// con una intensidad variable (similar a PrecipitationIntensity).
		/// </summary>
		private void UpdateEggRain(float dt)
		{
			// Acumulador de tiempo para espaciar la generación de huevos
			this.m_eggRainTimer += dt;

			// Intensidad de la lluvia de huevos (oscila con el tiempo para dar variedad)
			// Lerp entre 3 y 12 huevos por segundo aprox.
			float intensity = MathUtils.Lerp(3f, 12f, 0.5f * MathF.Sin(0.2f * (float)Time.FrameStartTime) + 0.5f);

			// Probabilidad por frame de soltar un huevo (depende de dt e intensidad)
			// A diferencia de la lluvia real (partículas), los huevos son Pickables pesados,
			// así que limitamos la cantidad para no sobrecargar el mundo.
			float probability = intensity * dt * 0.5f; // factor 0.5 para no saturar

			if (this.m_random.Float(0f, 1f) < probability)
			{
				this.SpawnFallingEgg();
			}

			// Cada cierto tiempo, lanzamos una ráfaga extra de huevos (como chubasco)
			if (this.m_eggRainTimer > 2.5f)
			{
				this.m_eggRainTimer = 0f;
				int burstCount = this.m_random.Int(2, 5);
				for (int i = 0; i < burstCount; i++)
				{
					this.SpawnFallingEgg();
				}
			}
		}

		/// <summary>
		/// Genera un huevo que cae del cielo imitando una gota de lluvia.
		/// Posición: alrededor del jugador, en un radio similar al de la lluvia (visibilidad).
		/// Altura: por encima del jugador para que tenga espacio de caída.
		/// Velocidad: hacia abajo con leve desplazamiento horizontal (efecto "viento").
		/// </summary>
		private void SpawnFallingEgg()
		{
			// Radio de caída alrededor del jugador (similar a m_rainSoundRadius = 7)
			// pero ampliado un poco para cubrir la zona visible.
			Vector2 horizontalOffset = this.m_random.Vector2(8f, 30f);

			Vector3 playerPos = this.m_componentPlayer.ComponentBody.Position;

			// Posición de spawn: encima del jugador, a altura suficiente
			// (similar a como los ejes de precipitación se extienden hacia arriba)
			float spawnHeight = playerPos.Y + this.m_random.Float(35f, 55f);

			Vector3 spawnPosition = new Vector3(
				playerPos.X + horizontalOffset.X,
				spawnHeight,
				playerPos.Z + horizontalOffset.Y
			);

			// Velocidad: hacia abajo, con leve viento horizontal (como gotas de lluvia con viento)
			Vector3 velocity = new Vector3(
				this.m_random.Float(-1.5f, 1.5f),
				this.m_random.Float(-8f, -5f),
				this.m_random.Float(-1.5f, 1.5f)
			);

			// Crear el valor del bloque del huevo
			int eggValue = Terrain.MakeBlockValue(this.m_eggBlockIndex, 0, 0);

			// Añadir como Pickable: caerá con gravedad igual que un objeto tirado
			this.m_subsystemPickables.AddPickable(eggValue, 1, spawnPosition, velocity, null);
		}

		// Implementación de IPlayerNoiseListener
		public void HearPlayerNoise(ComponentPlayer sourcePlayer, ComponentBody sourceBody, Vector3 sourcePosition, float loudness)
		{
			// Si la celebración está activa, el ruido es fuerte y no es emitido por nosotros mismos
			if (this.m_celebrationActive && sourcePlayer != null && sourcePlayer != this.m_componentPlayer && loudness >= 1f)
			{
				// Lanzamos un fuego artificial en la posición de quien hizo el ruido
				this.SpawnFirework(sourcePosition + new Vector3(0f, 1f, 0f));
			}
		}

		private void SpawnFirework(Vector3 position)
		{
			int data = 0;
			data = FireworksBlock.SetShape(data, (FireworksBlock.Shape)this.m_random.Int(0, 7));
			data = FireworksBlock.SetColor(data, this.m_random.Int(0, 7));
			data = FireworksBlock.SetAltitude(data, this.m_random.Int(0, 1));
			data = FireworksBlock.SetFlickering(data, this.m_random.Float(0f, 1f) < 0.25f);

			int value = Terrain.MakeBlockValue(215, 0, data);
			this.m_subsystemProjectiles.FireProjectile(value, position, new Vector3(this.m_random.Float(-3f, 3f), 45f, this.m_random.Float(-3f, 3f)), Vector3.Zero, null);
		}

		private SubsystemSky m_subsystemSky;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemPickables m_subsystemPickables;
		private ComponentGui m_componentGui;
		private ComponentPlayer m_componentPlayer;
		private ComponentSleep m_componentSleep;
		private Random m_random = new Random();

		private bool m_celebrationActive = false;
		private static int m_lastMessageYear = 0;

		// === Campos para la lluvia de huevos ===
		private int m_eggBlockIndex = -1;       // Índice del bloque ChristmasGiftEggBlock
		private float m_eggRainTimer = 0f;      // Acumulador para ráfagas
	}
}
