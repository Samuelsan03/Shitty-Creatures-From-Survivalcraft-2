using System;
using Engine;
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
			this.m_componentGui = base.Entity.FindComponent<ComponentGui>(true);
			this.m_componentPlayer = base.Entity.FindComponent<ComponentPlayer>(true);
			this.m_componentSleep = base.Entity.FindComponent<ComponentSleep>();
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
		private ComponentGui m_componentGui;
		private ComponentPlayer m_componentPlayer;
		private ComponentSleep m_componentSleep;
		private Random m_random = new Random();

		private bool m_celebrationActive = false;
		private static int m_lastMessageYear = 0;
	}
}
