using Engine;
using Engine.Graphics;
using Engine.Media;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentHealthBar : Component, IDrawable
	{
		public int[] DrawOrders
		{
			get
			{
				return ComponentHealthBar.s_drawOrders;
			}
		}

		public override void Load(ValuesDictionary values, IdToEntityMap map)
		{
			this.m_modelsRenderer = base.Project.FindSubsystem<SubsystemModelsRenderer>(true);
			this.m_creature = base.Entity.FindComponent<ComponentCreature>(true);
			this.m_health = base.Entity.FindComponent<ComponentHealth>(true);
			this.m_body = base.Entity.FindComponent<ComponentBody>(true);

			// Cargar MaxDisplayDistance del ValuesDictionary si está configurado
			this.MaxDisplayDistance = values.GetValue<float>("MaxDisplayDistance", 10f);
		}

		public void Draw(Camera camera, int drawOrder)
		{
			if (this.m_health == null || this.m_health.Health <= 0f)
			{
				return;
			}

			Vector3 vector = (this.m_body.BoundingBox.Min + this.m_body.BoundingBox.Max) * 0.5f;
			float num = this.m_body.BoundingBox.Max.Y - this.m_body.BoundingBox.Min.Y;

			if (Vector3.Distance(camera.ViewPosition, vector) > this.MaxDisplayDistance)
			{
				return;
			}

			// Punto para el texto (centrado sobre la entidad)
			Vector3 textPosition = new Vector3(vector.X, this.m_body.BoundingBox.Max.Y + num * 0.3f, vector.Z);

			// Punto para la barra (debajo del texto)
			Vector3 barPosition = new Vector3(vector.X, this.m_body.BoundingBox.Max.Y + num * 0.2f, vector.Z);

			if (Vector3.Dot(camera.ViewDirection, textPosition - camera.ViewPosition) <= 0f)
			{
				return;
			}
			if (Vector3.Dot(camera.ViewDirection, barPosition - camera.ViewPosition) <= 0f)
			{
				return;
			}

			Vector3 textViewPosition = Vector3.Transform(textPosition, camera.ViewMatrix);
			Vector3 barViewPosition = Vector3.Transform(barPosition, camera.ViewMatrix);

			Vector3 horizontalOffset = Vector3.TransformNormal(0.005f * Vector3.Normalize(Vector3.Cross(camera.ViewDirection, camera.ViewUp)), camera.ViewMatrix);
			Vector3 verticalOffset = Vector3.TransformNormal(-0.005f * Vector3.UnitY, camera.ViewMatrix);

			float healthPercent = MathUtils.Saturate(this.m_health.Health);
			float attackResilience = this.m_health.AttackResilience;
			float displayedHealth = this.m_health.Health * attackResilience;

			Color color = (healthPercent < 0.3f) ? Color.Red : ((healthPercent < 0.7f) ? Color.Yellow : Color.Green);

			// MODIFICADO: Usar LanguageControl con categoría "ComponentHealthBar"
			string hpText = LanguageControl.Get("HealthBar", "HP", "HP");
			string text = this.m_creature.DisplayName + " " + displayedHealth.ToString("0") + " " + hpText;

			BitmapFont bitmapFont = ContentManager.Get<BitmapFont>("Fonts/Pericles");
			FontBatch3D fontBatch3D = this.m_modelsRenderer.PrimitivesRenderer.FontBatch(bitmapFont, 1, DepthStencilState.DepthRead, RasterizerState.CullNoneScissor, BlendState.AlphaBlend, SamplerState.LinearClamp);

			// Dibujar texto centrado usando TextAnchor.Center
			fontBatch3D.QueueText(text, textViewPosition, horizontalOffset, verticalOffset, color, TextAnchor.Center);
			fontBatch3D.Flush(camera.ViewProjectionMatrix, false);

			// Dimensiones de la barra de salud
			float barWidth = 120f;
			float barHeight = 12.5f; // 2.5 veces más gruesa (original: 5f)

			Vector3 barStart = barViewPosition - horizontalOffset * (barWidth * 0.5f);
			Vector3 barEnd = barViewPosition + horizontalOffset * (barWidth * 0.5f);
			Vector3 barTop = barStart + verticalOffset * barHeight;
			Vector3 barTopEnd = barEnd + verticalOffset * barHeight;

			FlatBatch3D flatBatch3D = this.m_modelsRenderer.PrimitivesRenderer.FlatBatch(0, null, null, null);

			// Parte llena de la barra
			flatBatch3D.QueueQuad(barStart, barTop,
								  Vector3.Lerp(barTop, barTopEnd, healthPercent),
								  Vector3.Lerp(barStart, barEnd, healthPercent),
								  Color.Lerp(Color.Red, Color.Green, healthPercent));

			// Parte vacía de la barra (si no está llena)
			if (healthPercent < 1f)
			{
				flatBatch3D.QueueQuad(Vector3.Lerp(barStart, barEnd, healthPercent),
									  Vector3.Lerp(barTop, barTopEnd, healthPercent),
									  barTopEnd, barEnd, new Color(0, 0, 0, 180));
			}
			flatBatch3D.Flush(camera.ViewProjectionMatrix, false);
		}

		// ELIMINADO: Método GetLocalizedHPText personalizado - ahora usamos LanguageControl directamente

		private SubsystemModelsRenderer m_modelsRenderer;
		private ComponentCreature m_creature;
		private ComponentHealth m_health;
		private ComponentBody m_body;

		private static readonly int[] s_drawOrders = new int[]
		{
			1000
		};

		public float MaxDisplayDistance = 10f;
		private const float textOffsetFactor = 0.3f;
		private const float barOffsetFactor = 0.2f;
	}
}
