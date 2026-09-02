using System;
using System.Linq;
using System.Xml.Linq;
using Engine;
using Engine.Graphics;
using Engine.Media;

namespace Game
{
	public class EnchantedMessageWidget : StackPanelWidget
	{
		public EnchantedMessageWidget()
		{
			XElement node = ContentManager.Get<XElement>("Widgets/MessageWidget");
			this.LoadContents(this, node);
		}

		public void DisplayMessage(string text, Color color, bool blinking, float fontScale = 1f)
		{
			if (!string.IsNullOrEmpty(text))
			{
				this.DisplayMessage(new EnchantedMessageWidget.Message(text, color, blinking, fontScale, false));
			}
		}

		public void DisplayRainbowMessage(string text, bool blinking = false, float fontScale = 1f)
		{
			if (!string.IsNullOrEmpty(text))
			{
				this.DisplayMessage(new EnchantedMessageWidget.Message(text, Color.White, blinking, fontScale, true));
			}
		}

		public void DisplayMessage(EnchantedMessageWidget.Message message)
		{
			this.AddMessage(message);
			this.RemoveOldMessages();
		}

		public override void Update()
		{
			for (int i = this.m_messages.Count - 1; i >= 0; i--)
			{
				this.m_messages[i].Update();
			}
			this.RemoveOldMessages();
		}

		public void AddMessage(EnchantedMessageWidget.Message message)
		{
			this.m_messages.Add(message);
			this.Children.Add(message.LabelWidget);
		}

		public void RemoveMessage(EnchantedMessageWidget.Message message)
		{
			this.m_messages.Remove(message);
			this.Children.Remove(message.LabelWidget);
		}

		public void RemoveOldMessages()
		{
			for (int i = this.m_messages.Count - 1; i >= 0; i--)
			{
				EnchantedMessageWidget.Message message = this.m_messages[i];
				int num = this.m_messages.Count - i - 1;
				if (Time.FrameStartTime >= message.StartTime + (double)message.Duration || num >= 3 || (num > 0 && !message.Blinking))
				{
					this.RemoveMessage(message);
				}
			}
		}

		public const int MaxMessages = 3;

		public DynamicArray<EnchantedMessageWidget.Message> m_messages = new DynamicArray<EnchantedMessageWidget.Message>();

		public new class Message : MessageWidget.Message
		{
			private float m_rainbowHueOffset;

			public Message(string text, Color color, bool blinking, float fontScale = 1f, bool isRainbow = false)
				: base(text, color, blinking, fontScale)
			{
				this.IsRainbow = isRainbow;
				this.m_rainbowHueOffset = new Random().Float() * 360f;
			}

			public bool IsRainbow;

			public override void Update()
			{
				float num;
				if (this.Blinking)
				{
					num = MathUtils.Saturate(1f * (float)(this.StartTime + (double)this.Duration - Time.FrameStartTime));
					if (Time.FrameStartTime - this.StartTime < 0.417)
					{
						num *= MathUtils.Lerp(0.25f, 1f, 0.5f * (1f - MathF.Cos(37.699112f * (float)(Time.FrameStartTime - this.StartTime))));
					}
				}
				else
				{
					num = MathUtils.Saturate(MathUtils.Min(3f * (float)(Time.FrameStartTime - this.StartTime), 1f * (float)(this.StartTime + (double)this.Duration - Time.FrameStartTime)));
				}
				if (this.IsRainbow)
				{
					float elapsedSeconds = (float)(Time.FrameStartTime - this.StartTime);
					float hue = (this.m_rainbowHueOffset + elapsedSeconds * 60f) % 360f;
					Vector3 hsv = new Vector3(hue, 1f, 1f);
					Vector3 rgb = Color.HsvToRgb(hsv);
					Color rainbowColor = new Color(rgb.X, rgb.Y, rgb.Z);
					this.LabelWidget.Color = rainbowColor * num;
				}
				else
				{
					this.LabelWidget.Color = this.Color * num;
				}
			}
		}
	}
}
