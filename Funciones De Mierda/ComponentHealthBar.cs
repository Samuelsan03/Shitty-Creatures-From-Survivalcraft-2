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

			Vector3 vector2 = new Vector3(vector.X, this.m_body.BoundingBox.Max.Y + num * 0.3f, vector.Z);
			Vector3 vector3 = new Vector3(vector.X, this.m_body.BoundingBox.Max.Y + num * 0.2f, vector.Z);

			if (Vector3.Dot(camera.ViewDirection, vector2 - camera.ViewPosition) <= 0f)
			{
				return;
			}
			if (Vector3.Dot(camera.ViewDirection, vector3 - camera.ViewPosition) <= 0f)
			{
				return;
			}

			Vector3 vector4 = Vector3.Transform(vector2, camera.ViewMatrix);
			Vector3 vector5 = Vector3.Transform(vector3, camera.ViewMatrix);
			Vector3 vector6 = Vector3.TransformNormal(0.005f * Vector3.Normalize(Vector3.Cross(camera.ViewDirection, camera.ViewUp)), camera.ViewMatrix);
			Vector3 vector7 = Vector3.TransformNormal(-0.005f * Vector3.UnitY, camera.ViewMatrix);

			float num2 = MathUtils.Saturate(this.m_health.Health);
			float attackResilience = this.m_health.AttackResilience;
			float num3 = this.m_health.Health * attackResilience;

			// Determinar el color de la barra de salud según la vida
			Color healthColor;
			if (num2 < 0.3f)
				healthColor = Color.Red;       // Rojo cuando está a punto de morir
			else if (num2 < 0.7f)
				healthColor = Color.Yellow;    // Amarillo cuando está a medio
			else
				healthColor = Color.Green;     // Verde cuando está lleno

			Color textColor = (num2 < 0.3f) ? Color.Red : ((num2 < 0.7f) ? Color.Yellow : Color.Green);

			string text = this.m_creature.DisplayName + " " + num3.ToString("0") + " HP";

			BitmapFont bitmapFont = ContentManager.Get<BitmapFont>("Fonts/Pericles");
			FontBatch3D fontBatch3D = this.m_modelsRenderer.PrimitivesRenderer.FontBatch(bitmapFont, 1, DepthStencilState.DepthRead, RasterizerState.CullNoneScissor, BlendState.AlphaBlend, SamplerState.LinearClamp);

			// CORRECCIÓN: Usar 0 en lugar de TextAnchor
			fontBatch3D.QueueText(text, vector4, vector6, vector7, textColor, 0);
			fontBatch3D.Flush(camera.ViewProjectionMatrix, false);

			float num4 = 120f;
			float num5 = 5f;
			float outlineThickness = 1f; // Grosor del contorno
			
			Vector3 vector8 = vector6 * (num4 * 0.5f);
			Vector3 vector9 = vector7 * num5;
			Vector3 vector10 = vector5 - vector8;
			Vector3 vector11 = vector5 + vector8;
			Vector3 vector12 = vector10 + vector9;
			Vector3 vector13 = vector11 + vector9;

			FlatBatch3D flatBatch3D = this.m_modelsRenderer.PrimitivesRenderer.FlatBatch(0, null, null, null);
			
			// Calcular los vértices del contorno (rectángulo más grande)
			Vector3 outlineOffsetX = vector6 * outlineThickness;
			Vector3 outlineOffsetY = vector7 * outlineThickness;
			
			// Vértices del contorno exterior
			Vector3 outlineBottomLeft = vector10 - outlineOffsetX - outlineOffsetY;
			Vector3 outlineBottomRight = vector11 + outlineOffsetX - outlineOffsetY;
			Vector3 outlineTopLeft = vector12 - outlineOffsetX + outlineOffsetY;
			Vector3 outlineTopRight = vector13 + outlineOffsetX + outlineOffsetY;
			
			// Dibujar el contorno blanco
			flatBatch3D.QueueQuad(outlineBottomLeft, outlineTopLeft, outlineTopRight, outlineBottomRight, new Color(255, 255, 255, 200));
			
			// Dibujar el fondo de la barra (parte negra detrás de la salud)
			flatBatch3D.QueueQuad(vector10, vector12, vector13, vector11, new Color(0, 0, 0, 180));
			
			// Barra de salud principal con el color correspondiente según la vida
			if (num2 > 0f)
			{
				Vector3 healthEnd = Vector3.Lerp(vector10, vector11, num2);
				Vector3 healthEndTop = Vector3.Lerp(vector12, vector13, num2);
				flatBatch3D.QueueQuad(vector10, vector12, healthEndTop, healthEnd, healthColor);
			}
			
			flatBatch3D.Flush(camera.ViewProjectionMatrix, false);
		}

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
