using System;
using Engine;
using Engine.Graphics;
using Engine.Media;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemCreatureHealthBar : Subsystem, IDrawable, IUpdateable
	{
		public enum CreatureHealthBarState
		{
			Alive,
			Partial,
			AboutToDie,
			Dead
		}

		public int[] DrawOrders
		{
			get
			{
				return this.m_drawOrders;
			}
		}

		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			this.m_primitivesRenderer = new PrimitivesRenderer3D();
			this.m_subsystemCreatureSpawn = base.Project.FindSubsystem<SubsystemCreatureSpawn>(true);
		}

		public virtual void Draw(Camera camera, int drawOrder)
		{
			if (drawOrder != this.m_drawOrders[0])
			{
				return;
			}

			if (!ShittyCreaturesSettingsManager.HealthBarEnabled)
			{
				return;
			}

			Matrix invertedViewMatrix = camera.InvertedViewMatrix;

			Vector3 right = new Vector3(invertedViewMatrix.M11, invertedViewMatrix.M12, invertedViewMatrix.M13);
			Vector3 up = new Vector3(invertedViewMatrix.M21, invertedViewMatrix.M22, invertedViewMatrix.M23);
			Vector3 forward = new Vector3(invertedViewMatrix.M31, invertedViewMatrix.M32, invertedViewMatrix.M33);

			FlatBatch3D flatBatch = this.m_primitivesRenderer.FlatBatch(m_drawOrders[0], DepthStencilState.DepthRead, RasterizerState.CullNoneScissor);
			FontBatch3D fontBatch = this.m_primitivesRenderer.FontBatch(LabelWidget.BitmapFont, m_drawOrders[0], DepthStencilState.DepthRead, RasterizerState.CullNoneScissor, BlendState.AlphaBlend, SamplerState.LinearClamp);

			foreach (ComponentCreature componentCreature in this.m_subsystemCreatureSpawn.Creatures)
			{
				ComponentBody componentBody = componentCreature.ComponentBody;
				ComponentHealth componentHealth = componentCreature.ComponentHealth;

				if (componentBody == null || componentHealth == null)
				{
					continue;
				}

				// ============================================
				// LÓGICA MEJORADA: Ocultar SOLO si es primera persona real
				// ============================================
				ComponentPlayer componentPlayer = componentCreature.Entity.FindComponent<ComponentPlayer>();

				if (componentPlayer != null)
				{
					// Calculamos la distancia entre la cámara y el cuerpo del jugador
					float distance = Vector3.Distance(camera.ViewPosition, componentBody.Position);

					// Si la distancia es menor a 1.5 bloques, significa que la cámara
					// está dentro de la cabeza/cuerpo (Primera Persona o Cámara Fija clavada).
					// En estas situaciones el modelo del jugador no se dibuja, por lo tanto
					// tampoco debemos dibujar la barra de vida flotando en el aire.
					if (distance < 1.5f)
					{
						continue;
					}
				}

				Vector3 position = componentBody.Position;
				float height = componentBody.StanceBoxSize.Y;

				position.Y += height + 0.095f;
				position -= forward * 0.2f;

				float barWidth = 0.75f;
				float barHeight = 0.1f;

				CreatureHealthBarState state = this.GetState(componentHealth.Health);
				Color barColor = this.GetColor(state);

				float halfWidth = barWidth / 2f;
				float halfHeight = barHeight / 2f;

				// Dibujar el fondo negro
				Vector3 v0 = position - right * halfWidth - up * halfHeight;
				Vector3 v1 = position + right * halfWidth - up * halfHeight;
				Vector3 v2 = position + right * halfWidth + up * halfHeight;
				Vector3 v3 = position - right * halfWidth + up * halfHeight;

				flatBatch.QueueQuad(v0, v1, v2, v3, new Color(0, 0, 0, 200));

				// Dibujar la barra de vida coloreada
				float currentHealth = componentHealth.Health;
				float coloredWidth = barWidth * currentHealth;

				if (coloredWidth > 0.001f)
				{
					float coloredHalfWidth = coloredWidth / 2f;
					float offsetAmount = (coloredWidth - barWidth) / 2f;
					Vector3 offset = right * offsetAmount;
					Vector3 fgCenter = position + offset;

					float fgHalfHeight = halfHeight;

					Vector3 fv0 = fgCenter - right * coloredHalfWidth - up * fgHalfHeight;
					Vector3 fv1 = fgCenter + right * coloredHalfWidth - up * fgHalfHeight;
					Vector3 fv2 = fgCenter + right * coloredHalfWidth + up * fgHalfHeight;
					Vector3 fv3 = fgCenter - right * coloredHalfWidth + up * fgHalfHeight;

					flatBatch.QueueQuad(fv0, fv1, fv2, fv3, barColor);
				}

				// Calcular la vida real
				float actualHealth = currentHealth * componentHealth.AttackResilience;

				// Dibujar el texto encima de la barra
				string creatureName = componentCreature.DisplayName;
				string healthText = creatureName + " " + LanguageControl.Get(new string[] { "HealthBar", "HP" }) + ": " + actualHealth.ToString("F2");

				Vector3 textPosition = position + up * (halfHeight + 0.085f);

				float textScale = 0.0035f;
				Vector3 textRight = right * textScale;
				Vector3 textUp = -up * textScale;

				fontBatch.QueueText(healthText, textPosition, textRight, textUp, barColor, TextAnchor.HorizontalCenter);
			}

			Matrix viewProjectionMatrix = camera.ViewMatrix * camera.ProjectionMatrix;
			this.m_primitivesRenderer.Flush(viewProjectionMatrix, true, m_drawOrders[0]);
		}

		public virtual void Update(float dt)
		{
		}

		public CreatureHealthBarState GetState(float health)
		{
			if (health > 0.6f) return CreatureHealthBarState.Alive;
			if (health > 0.3f) return CreatureHealthBarState.Partial;
			if (health > 0f) return CreatureHealthBarState.AboutToDie;
			return CreatureHealthBarState.Dead;
		}

		public Color GetColor(CreatureHealthBarState state)
		{
			switch (state)
			{
				case CreatureHealthBarState.Alive: return Color.Green;
				case CreatureHealthBarState.Partial: return Color.Yellow;
				case CreatureHealthBarState.AboutToDie: return Color.Red;
				case CreatureHealthBarState.Dead: return Color.DarkRed;
				default: return Color.White;
			}
		}

		public int[] m_drawOrders = new int[]
		{
			250
		};

		public PrimitivesRenderer3D m_primitivesRenderer;
		public SubsystemCreatureSpawn m_subsystemCreatureSpawn;
	}
}
