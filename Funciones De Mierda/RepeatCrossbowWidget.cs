using System;
using System.Reflection.Metadata;
using System.Xml.Linq;
using Engine;
using Game;
using TemplatesDatabase;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Game
{
	public class RepeatCrossbowWidget : CanvasWidget
	{
		public RepeatCrossbowWidget(IInventory inventory, int slotIndex)
		{
			this.m_inventory = inventory;
			this.m_slotIndex = slotIndex;
			this.LoadContents(this, ContentManager.Get<XElement>("Widgets/RepeatCrossbowWidget"));
			this.m_inventoryGrid = this.Children.Find<GridPanelWidget>("InventoryGrid", true);
			this.m_inventorySlotWidget = this.Children.Find<InventorySlotWidget>("InventorySlot", true);
			this.m_instructionsLabel = this.Children.Find<LabelWidget>("InstructionsLabel", true);
			this.m_titleLabel = this.Children.Find<LabelWidget>("TitleLabel", true);
			this.m_inventoryLabel = this.Children.Find<LabelWidget>("InventoryLabel", true);

			// Set translated texts - ¡CORREGIDO! Usando GetContentWidgets
			this.m_titleLabel.Text = LanguageControl.GetContentWidgets("RepeatCrossbowWidget", "Title");
			this.m_inventoryLabel.Text = LanguageControl.GetContentWidgets("RepeatCrossbowWidget", "Inventory");

			for (int i = 0; i < this.m_inventoryGrid.RowsCount; i++)
			{
				for (int j = 0; j < this.m_inventoryGrid.ColumnsCount; j++)
				{
					InventorySlotWidget widget = new InventorySlotWidget();
					this.m_inventoryGrid.Children.Add(widget);
					this.m_inventoryGrid.SetWidgetCell(widget, new Point2(j, i));
				}
			}
			int num = 10;
			foreach (Widget widget2 in this.m_inventoryGrid.Children)
			{
				InventorySlotWidget inventorySlotWidget = widget2 as InventorySlotWidget;
				if (inventorySlotWidget != null)
				{
					inventorySlotWidget.AssignInventorySlot(inventory, num++);
				}
			}
			this.m_inventorySlotWidget.AssignInventorySlot(inventory, slotIndex);
			this.m_inventorySlotWidget.CustomViewMatrix = new Matrix?(Matrix.CreateLookAt(new Vector3(0f, 1f, 0.2f), new Vector3(0f, 0f, 0.2f), -Vector3.UnitZ));
		}

		public override void Update()
		{
			int slotValue = this.m_inventory.GetSlotValue(this.m_slotIndex);
			int slotCount = this.m_inventory.GetSlotCount(this.m_slotIndex);
			int num = Terrain.ExtractContents(slotValue);
			int data = Terrain.ExtractData(slotValue);
			int draw = RepeatCrossbowBlock.GetDraw(data);
			RepeatArrowBlock.ArrowType? arrowType = RepeatCrossbowBlock.GetArrowType(data);
			if (!(BlocksManager.Blocks[num] is RepeatCrossbowBlock) || slotCount <= 0)
			{
				base.ParentWidget.Children.Remove(this);
				return;
			}
			if (draw < 15)
			{
				this.m_instructionsLabel.Text = LanguageControl.GetContentWidgets("RepeatCrossbowWidget", "InstructionsPull");
			}
			else if (arrowType == null)
			{
				this.m_instructionsLabel.Text = LanguageControl.GetContentWidgets("RepeatCrossbowWidget", "InstructionsInsert");
			}
			else
			{
				this.m_instructionsLabel.Text = string.Format(LanguageControl.GetContentWidgets("RepeatCrossbowWidget", "InstructionsArrows"), RepeatCrossbowBlock.GetLoadCount(slotValue).ToString());
			}
			if ((draw < 15 || arrowType == null) && base.Input.Tap != null && this.HitTestGlobal(base.Input.Tap.Value, null) == this.m_inventorySlotWidget)
			{
				InventorySlotWidget inventorySlotWidget = this.m_inventorySlotWidget;
				Vector2? press = base.Input.Press;
				if (press != null)
				{
					Vector2 vector = inventorySlotWidget.ScreenToWidget(press.Value);
					float value = vector.Y - RepeatCrossbowWidget.DrawToPosition(draw);
					if ((double)Math.Abs(vector.X - this.m_inventorySlotWidget.ActualSize.X / 2f) < 25.0 && (double)Math.Abs(value) < 25.0)
					{
						this.m_dragStartOffset = new float?(value);
					}
				}
			}
			if (this.m_dragStartOffset == null)
			{
				return;
			}
			if (base.Input.Press != null)
			{
				InventorySlotWidget inventorySlotWidget2 = this.m_inventorySlotWidget;
				Vector2? press = base.Input.Press;
				if (press != null)
				{
					int num2 = RepeatCrossbowWidget.PositionToDraw(inventorySlotWidget2.ScreenToWidget(press.Value).Y - this.m_dragStartOffset.Value);
					this.SetDraw(num2);
					if (draw > 9 || num2 <= 9)
					{
						return;
					}
				}
				AudioManager.PlaySound("Audio/CrossbowDraw", 1f, this.m_random.Float(-0.2f, 0.2f), 0f);
				return;
			}
			this.m_dragStartOffset = null;
			if (draw == 15)
			{
				AudioManager.PlaySound("Audio/UI/ItemMoved", 1f, 0f, 0f);
				return;
			}
			this.SetDraw(0);
			AudioManager.PlaySound("Audio/CrossbowBoing", MathUtils.Saturate((float)(draw - 3) / 10f), this.m_random.Float(-0.1f, 0.1f), 0f);
		}

		public void SetDraw(int draw)
		{
			int slotValue = this.m_inventory.GetSlotValue(this.m_slotIndex);
			this.m_inventory.RemoveSlotItems(this.m_slotIndex, 1);
			this.m_inventory.AddSlotItems(this.m_slotIndex, Terrain.MakeBlockValue(RepeatCrossbowBlock.Index, 0, RepeatCrossbowBlock.SetDraw(Terrain.ExtractData(slotValue), draw)), 1);
		}

		public static float DrawToPosition(int draw)
		{
			return (float)((double)draw * 5.4 + 85.0);
		}

		public static int PositionToDraw(float position)
		{
			return (int)Math.Clamp(Math.Round((double)((float)(((double)position - 85.0) / 5.4))), 0.0, 15.0);
		}

		public IInventory m_inventory;

		public int m_slotIndex;

		public float? m_dragStartOffset;

		public GridPanelWidget m_inventoryGrid;

		public InventorySlotWidget m_inventorySlotWidget;

		public LabelWidget m_instructionsLabel;

		public LabelWidget m_titleLabel;

		public LabelWidget m_inventoryLabel;

		public Game.Random m_random = new Game.Random();

		public static string fName = "RepeatCrossbowWidget";
	}
}