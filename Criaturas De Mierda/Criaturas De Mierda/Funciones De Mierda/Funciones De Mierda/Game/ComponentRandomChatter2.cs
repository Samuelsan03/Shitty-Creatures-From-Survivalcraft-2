using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Engine.Media;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentRandomChatter2 : Component, IUpdateable, IDrawable
	{
		public int[] DrawOrders
		{
			get
			{
				return new int[] { 2000 };
			}
		}

		public float ActivationDistance { get; set; }
		public float DisplayDistance { get; set; }
		public float MinInterval { get; set; }
		public float MaxInterval { get; set; }
		public float RiseDuration { get; set; }
		public float FadeDuration { get; set; }

		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		public override void Load(ValuesDictionary values, IdToEntityMap map)
		{
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);
			this.m_subsystemModelsRenderer = base.Project.FindSubsystem<SubsystemModelsRenderer>(true);
			this.m_componentBody = base.Entity.FindComponent<ComponentBody>(true);
			this.m_font = ContentManager.Get<BitmapFont>("Fonts/Pericles");

			this.ActivationDistance = values.GetValue<float>("ActivationDistance", 5f);
			this.DisplayDistance = values.GetValue<float>("DisplayDistance", 10f);
			this.MinInterval = values.GetValue<float>("MinInterval", 8f);
			this.MaxInterval = values.GetValue<float>("MaxInterval", 30f);
			this.RiseDuration = values.GetValue<float>("RiseDuration", 2f);
			this.FadeDuration = values.GetValue<float>("FadeDuration", 0.5f);

			this.SetNextChatterTime();
		}

		public void Update(float dt)
		{
			double gameTime = this.m_subsystemTime.GameTime;

			if (this.m_currentState == State.Inactive)
			{
				if (gameTime > this.m_nextChatterTime)
				{
					bool playerNear = false;
					foreach (PlayerData playerData in this.m_subsystemPlayers.PlayersData)
					{
						ComponentPlayer componentPlayer = playerData.ComponentPlayer;
						if (((componentPlayer != null) ? componentPlayer.ComponentBody : null) != null &&
							Vector3.DistanceSquared(this.m_componentBody.Position, playerData.ComponentPlayer.ComponentBody.Position) < this.ActivationDistance * this.ActivationDistance)
						{
							playerNear = true;
							break;
						}
					}
					if (playerNear)
					{
						this.m_activePhrase = m_phrases[this.m_random.Int(0, m_phrases.Count - 1)];
						this.m_currentState = State.Rising;
						this.m_riseStartTime = gameTime;
						this.SetNextChatterTime();
						return;
					}
					this.m_nextChatterTime = gameTime + 1.0;
					return;
				}
			}
			else if (this.m_currentState == State.Rising)
			{
				double riseProgress = (gameTime - this.m_riseStartTime) / this.RiseDuration;

				if (riseProgress >= 1f)
				{
					this.m_currentState = State.FadeOut;
					this.m_fadeStartTime = gameTime;
				}
			}
			else if (this.m_currentState == State.FadeOut)
			{
				if (gameTime > this.m_fadeStartTime + this.FadeDuration)
				{
					this.m_currentState = State.Inactive;
					this.m_activePhrase = null;
				}
			}
		}

		public void Draw(Camera camera, int drawOrder)
		{
			if (this.m_currentState == State.Inactive || string.IsNullOrEmpty(this.m_activePhrase))
			{
				return;
			}

			// Verificar distancia de visualización
			bool playerInRange = false;
			foreach (PlayerData playerData in this.m_subsystemPlayers.PlayersData)
			{
				ComponentPlayer componentPlayer = playerData.ComponentPlayer;
				if (((componentPlayer != null) ? componentPlayer.ComponentBody : null) != null &&
					Vector3.DistanceSquared(this.m_componentBody.Position, playerData.ComponentPlayer.ComponentBody.Position) < this.DisplayDistance * this.DisplayDistance)
				{
					playerInRange = true;
					break;
				}
			}

			if (!playerInRange)
				return;

			// Calcular alpha basado en el estado actual
			float alpha = 1f;
			double currentTime = this.m_subsystemTime.GameTime;

			if (this.m_currentState == State.FadeOut)
			{
				double fadeProgress = (currentTime - this.m_fadeStartTime) / this.FadeDuration;
				alpha = 1f - MathUtils.Clamp((float)fadeProgress, 0f, 1f);
			}

			// Calcular progreso del ascenso
			double riseProgress = (currentTime - this.m_riseStartTime) / this.RiseDuration;
			float progress = MathUtils.Clamp((float)riseProgress, 0f, 1f);

			FontBatch3D fontBatch = this.m_subsystemModelsRenderer.PrimitivesRenderer.FontBatch(this.m_font, 1, DepthStencilState.None, RasterizerState.CullNoneScissor, BlendState.AlphaBlend, SamplerState.LinearClamp);

			Vector3 center = (this.m_componentBody.BoundingBox.Min + this.m_componentBody.BoundingBox.Max) * 0.5f;
			float height = this.m_componentBody.BoundingBox.Max.Y - this.m_componentBody.BoundingBox.Min.Y;

			// Posición que asciende desde las rodillas hasta la cabeza
			float startHeight = height * 0.3f; // Rodillas
			float endHeight = height * 0.9f;   // Cabeza
			float currentHeight = startHeight + (endHeight - startHeight) * progress;

			Vector3 position = new Vector3(center.X, center.Y + currentHeight, center.Z);

			Vector3 right = Vector3.Normalize(Vector3.Cross(camera.ViewDirection, camera.ViewUp));
			float scale = 0.005f;

			Vector3 screenPos = Vector3.Transform(position, camera.ViewMatrix);
			Vector3 rightVec = Vector3.TransformNormal(right, camera.ViewMatrix) * scale;
			Vector3 downVec = Vector3.TransformNormal(-Vector3.UnitY, camera.ViewMatrix) * scale;

			Color color = Color.White * alpha;
			fontBatch.QueueText(this.m_activePhrase, screenPos, rightVec, downVec, color, TextAnchor.Center);
			fontBatch.Flush(camera.ViewProjectionMatrix, false);
		}

		private void SetNextChatterTime()
		{
			this.m_nextChatterTime = this.m_subsystemTime.GameTime + (double)this.m_random.Float(this.MinInterval, this.MaxInterval);
		}

		private SubsystemTime m_subsystemTime;
		private SubsystemPlayers m_subsystemPlayers;
		private SubsystemModelsRenderer m_subsystemModelsRenderer;
		private ComponentBody m_componentBody;
		private BitmapFont m_font;
		private Game.Random m_random = new Game.Random();
		private State m_currentState;
		private string m_activePhrase;
		private double m_riseStartTime;
		private double m_fadeStartTime;
		private double m_nextChatterTime;

		private enum State
		{
			Inactive,
			Rising,
			FadeOut
		}

		private static readonly List<string> m_phrases = new List<string>
		{
			"Alianza es una mierda\r\nMerece ser exterminada",
			"A la mierda Alianza",
			"Los Digimons merecen ser exterminados",
			"Viva Muerte\r\nAbajo Alianza",
			"Oh yeah, fuck, I'm coming!",
			"Yeah Baby",
			"Viva el señor Mencho",
			"El problema es que somos demasiados",
			"Muerte ya esta aquí",
			"El infierno morado es el mejor paraíso",
			"Te vamos a follar zorra",
			"Somos tus machos a joderte la vida she",
			"Fuck, esto es lo mejor",
			"Oh yeah baby",
			"Chinga tu madre, motherfucker",
			"Son a bitch",
			"Yo te boté\r\nTe di banda y te solté, yo te solté\r\nPa'l carajo te mandé, yo te mandé\r\nY a tu amiga me clavé, me la clavé\r\nFuck you, hijueputa, yeh",
			"Vete a la VRG",
			"Criminal, cri-criminal\r\nTu estilo, tu flow, mami, muy criminal"
		};
	}
}
