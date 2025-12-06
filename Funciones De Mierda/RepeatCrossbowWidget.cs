using System;
using System.Xml.Linq;
using Engine;
using Game;

namespace Game
{
	// Token: 0x020000FF RID: 255
	public class RepeatCrossbowWidget : CanvasWidget
	{
		// Token: 0x0600072E RID: 1838 RVA: 0x00033F68 File Offset: 0x00032168
		public RepeatCrossbowWidget(IInventory inventory, int slotIndex)
		{
			this.m_inventory = inventory;
			this.m_slotIndex = slotIndex;
			this.LoadContents(this, ContentManager.Get<XElement>("Widgets/RepeatCrossbowWidget"));
			this.m_inventoryGrid = this.Children.Find<GridPanelWidget>("InventoryGrid", true);
			this.m_inventorySlotWidget = this.Children.Find<InventorySlotWidget>("InventorySlot", true);
			this.m_instructionsLabel = this.Children.Find<LabelWidget>("InstructionsLabel", true);
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

		// Token: 0x0600072F RID: 1839 RVA: 0x000340F4 File Offset: 0x000322F4
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
				this.m_instructionsLabel.Text = LanguageControl.Get(RepeatCrossbowWidget.fName, 0);
			}
			else if (arrowType == null)
			{
				this.m_instructionsLabel.Text = LanguageControl.Get(RepeatCrossbowWidget.fName, 1);
			}
			else
			{
				this.m_instructionsLabel.Text = LanguageControl.Get(RepeatCrossbowWidget.fName, 2) + RepeatCrossbowBlock.GetLoadCount(slotValue).ToString() + "/8";
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

		// Token: 0x06000730 RID: 1840 RVA: 0x000343C0 File Offset: 0x000325C0
		public void SetDraw(int draw)
		{
			int slotValue = this.m_inventory.GetSlotValue(this.m_slotIndex);
			this.m_inventory.RemoveSlotItems(this.m_slotIndex, 1);
			this.m_inventory.AddSlotItems(this.m_slotIndex, Terrain.MakeBlockValue(RepeatCrossbowBlock.Index, 0, RepeatCrossbowBlock.SetDraw(Terrain.ExtractData(slotValue), draw)), 1);
		}

		// Token: 0x06000731 RID: 1841 RVA: 0x0003441B File Offset: 0x0003261B
		public static float DrawToPosition(int draw)
		{
			return (float)((double)draw * 5.4 + 85.0);
		}

		// Token: 0x06000732 RID: 1842 RVA: 0x00034434 File Offset: 0x00032634
		public static int PositionToDraw(float position)
		{
			return (int)Math.Clamp(Math.Round((double)((float)(((double)position - 85.0) / 5.4))), 0.0, 15.0);
		}

		// Token: 0x04000448 RID: 1096
		public IInventory m_inventory;

		// Token: 0x04000449 RID: 1097
		public int m_slotIndex;

		// Token: 0x0400044A RID: 1098
		public float? m_dragStartOffset;

		// Token: 0x0400044B RID: 1099
		public GridPanelWidget m_inventoryGrid;

		// Token: 0x0400044C RID: 1100
		public InventorySlotWidget m_inventorySlotWidget;

		// Token: 0x0400044D RID: 1101
		public LabelWidget m_instructionsLabel;

		// Token: 0x0400044E RID: 1102
		public Game.Random m_random = new Game.Random();

		// Token: 0x0400044F RID: 1103
		public static string fName = "RepeatCrossbowWidget";
	}
}
