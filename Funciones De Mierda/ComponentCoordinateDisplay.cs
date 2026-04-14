using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentCoordinateDisplay : Component, IUpdateable
	{
		private ComponentPlayer m_componentPlayer;
		private ComponentGui m_componentGui;

		// 用于轮廓的双标签
		private LabelWidget m_shadowLabel;
		private LabelWidget m_foregroundLabel;
		private CanvasWidget m_labelContainer;
		private ContainerWidget m_targetContainer;

		// 本地化格式字符串
		private string m_coordinateFormat;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_componentPlayer = Entity.FindComponent<ComponentPlayer>(true);
			m_componentGui = Entity.FindComponent<ComponentGui>(true);

			if (m_componentPlayer == null || m_componentGui == null)
			{
				Log.Warning("ComponentCoordinateDisplay: No se encontró ComponentPlayer o ComponentGui.");
				return;
			}

			// 从语言文件获取格式化字符串，失败则使用默认值
			if (!LanguageControl.TryGet(out m_coordinateFormat, "ComponentCoordinateDisplay", "CoordinateFormat"))
			{
				m_coordinateFormat = "Coordenadas: X: {0:F1}  Y: {1:F1}  Z: {2:F1}";
			}

			// 1. 创建轮廓标签（黑色，偏移一点）
			m_shadowLabel = new LabelWidget
			{
				Font = LabelWidget.BitmapFont,
				Color = Color.Black,
				HorizontalAlignment = WidgetAlignment.Center,
				VerticalAlignment = WidgetAlignment.Center,
				Margin = new Vector2(1, 1),
				FontScale = 0.5f,   // 字体缩放为 0.5 倍
				Text = string.Format(m_coordinateFormat, 0f, 0f, 0f)
			};

			// 2. 创建前景标签（白色，居中）
			m_foregroundLabel = new LabelWidget
			{
				Font = LabelWidget.BitmapFont,
				Color = Color.White,
				HorizontalAlignment = WidgetAlignment.Center,
				VerticalAlignment = WidgetAlignment.Center,
				FontScale = 0.5f,   // 字体缩放为 0.5 倍
				Text = string.Format(m_coordinateFormat, 0f, 0f, 0f)
			};

			// 3. 将两个标签放入一个 CanvasWidget 中，以便整体定位
			m_labelContainer = new CanvasWidget
			{
				HorizontalAlignment = WidgetAlignment.Far,
				VerticalAlignment = WidgetAlignment.Far,
				Margin = new Vector2(10, 10)
			};
			m_labelContainer.Children.Add(m_shadowLabel);
			m_labelContainer.Children.Add(m_foregroundLabel);

			// 4. 将容器添加到 GUI 的适当位置（优先右侧容器）
			ContainerWidget controlsContainer = m_componentGui.ControlsContainerWidget;
			if (controlsContainer != null)
			{
				var rightContainer = controlsContainer.Children.Find<ContainerWidget>("RightControlsContainer", true);
				if (rightContainer != null)
				{
					rightContainer.Children.Add(m_labelContainer);
					m_targetContainer = rightContainer;
				}
				else
				{
					controlsContainer.Children.Add(m_labelContainer);
					m_targetContainer = controlsContainer;
				}
			}
			else
			{
				m_componentPlayer.GuiWidget.Children.Add(m_labelContainer);
				m_targetContainer = m_componentPlayer.GuiWidget;
			}

			// 确保容器对齐方式正确
			m_labelContainer.HorizontalAlignment = WidgetAlignment.Far;
			m_labelContainer.VerticalAlignment = WidgetAlignment.Far;

			// --- VISIBILIDAD: inicializar según configuración ---
			UpdateVisibility();
			// ----------------------------------------------------
		}

		public void Update(float dt)
		{
			if (m_componentPlayer == null || m_foregroundLabel == null || m_shadowLabel == null)
				return;

			// --- VISIBILIDAD: aplicar el estado actual de la configuración ---
			UpdateVisibility();
			// ----------------------------------------------------------------

			var body = m_componentPlayer.ComponentBody;
			if (body != null)
			{
				Vector3 pos = body.Position;
				string text = string.Format(m_coordinateFormat, pos.X, pos.Y, pos.Z);
				m_foregroundLabel.Text = text;
				m_shadowLabel.Text = text;
			}
		}

		// --- NUEVO MÉTODO PARA CONTROLAR VISIBILIDAD ---
		private void UpdateVisibility()
		{
			bool shouldShow = ShittyCreaturesSettingsManager.CoordinateDisplayEnabled;
			if (m_labelContainer != null)
			{
				m_labelContainer.IsVisible = shouldShow;
			}
		}
		// ------------------------------------------------

		public override void Dispose()
		{
			if (m_labelContainer != null && m_targetContainer != null)
			{
				m_targetContainer.Children.Remove(m_labelContainer);
			}
			base.Dispose();
		}
	}
}
