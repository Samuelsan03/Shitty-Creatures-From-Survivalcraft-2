using System;
using Engine;
using Engine.Graphics;
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

			// ELIMINADO: No necesitamos agregarlo manualmente. 
			// SubsystemDrawing ya lo detecta automáticamente porque implementamos IDrawable.
		}

		public virtual void Draw(Camera camera, int drawOrder)
		{
			if (drawOrder != this.m_drawOrders[0])
			{
				return;
			}

			Matrix invertedViewMatrix = camera.InvertedViewMatrix;

			Vector3 right = new Vector3(invertedViewMatrix.M11, invertedViewMatrix.M12, invertedViewMatrix.M13);
			Vector3 up = new Vector3(invertedViewMatrix.M21, invertedViewMatrix.M22, invertedViewMatrix.M23);
			Vector3 forward = new Vector3(invertedViewMatrix.M31, invertedViewMatrix.M32, invertedViewMatrix.M33);

			FlatBatch3D flatBatch = this.m_primitivesRenderer.FlatBatch(m_drawOrders[0], DepthStencilState.DepthRead, RasterizerState.CullNoneScissor);

			foreach (ComponentCreature componentCreature in this.m_subsystemCreatureSpawn.Creatures)
			{
				if (componentCreature.Entity.FindComponent<ComponentPlayer>() != null)
				{
					continue;
				}

				ComponentBody componentBody = componentCreature.ComponentBody;
				ComponentHealth componentHealth = componentCreature.ComponentHealth;

				if (componentBody == null || componentHealth == null)
				{
					continue;
				}

				Vector3 position = componentBody.Position;
				float height = componentBody.StanceBoxSize.Y;

				position.Y += height + 0.4f;
				position -= forward * 0.2f;

				float barWidth = 0.35f;
				float barHeight = 0.12f;

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
