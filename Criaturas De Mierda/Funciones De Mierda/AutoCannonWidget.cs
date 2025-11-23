using System;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Engine;
using Game;
using GameEntitySystem;

// Token: 0x02000002 RID: 2
[NullableContext(1)]
[Nullable(0)]
public class AutoCannonWidget : CanvasWidget
{
	// Token: 0x06000001 RID: 1 RVA: 0x00002050 File Offset: 0x00000250
	public AutoCannonWidget(ComponentPlayer componentPlayer, int slotIndex)
	{
		this.m_componentPlayer = componentPlayer;
		this.m_inventory = componentPlayer.ComponentMiner.Inventory;
		this.m_slotIndex = slotIndex;
		this.m_fuelInventory = new AutoCannonWidget.FuelInventory(this.m_inventory.Project);
		XElement xelement = ContentManager.Get<XElement>("Widgets/AutoCannonWidget");
		base.LoadContents(this, xelement);
		this.m_speedSlider = this.Children.Find<SliderWidget>("ProjectileSpeedSlider", true);
		this.m_rateSlider = this.Children.Find<SliderWidget>("FireRateSlider", true);
		this.m_spreadSlider = this.Children.Find<SliderWidget>("ProjectileSpreadSlider", true);
		this.m_speedTextBox = this.Children.Find<TextBoxWidget>("ProjectileSpeedTextBox", true);
		this.m_rateTextBox = this.Children.Find<TextBoxWidget>("FireRateTextBox", true);
		this.m_spreadTextBox = this.Children.Find<TextBoxWidget>("ProjectileSpreadTextBox", true);
		this.m_okButton = this.Children.Find<ButtonWidget>("OkButton", true);
		this.m_helpButton = this.Children.Find<ButtonWidget>("HelpButton", true);
		this.m_cancelButton = this.Children.Find<ButtonWidget>("CancelButton", true);
		this.m_inventorySlotWidget = this.Children.Find<InventorySlotWidget>("InventorySlot", true);
		this.m_inventorySlotWidget.AssignInventorySlot(this.m_inventory, slotIndex);
		GridPanelWidget gridPanelWidget = this.Children.Find<GridPanelWidget>("InventoryGrid", true);
		this.m_fuelSlot = this.Children.Find<InventorySlotWidget>("FuelSlot", true);
		this.m_fuelSlot.AssignInventorySlot(this.m_fuelInventory, 0);
		this.m_speedTextBox.Text = this.m_speedSlider.Value.ToString("0.##");
		this.m_rateTextBox.Text = this.m_rateSlider.Value.ToString("0.##");
		this.m_spreadTextBox.Text = this.m_spreadSlider.Value.ToString("0.##");
		for (int i = 0; i < gridPanelWidget.RowsCount; i++)
		{
			for (int j = 0; j < gridPanelWidget.ColumnsCount; j++)
			{
				InventorySlotWidget inventorySlotWidget = new InventorySlotWidget();
				gridPanelWidget.Children.Add(inventorySlotWidget);
				gridPanelWidget.SetWidgetCell(inventorySlotWidget, new Point2(j, i));
				inventorySlotWidget.AssignInventorySlot(this.m_inventory, 10 + i * gridPanelWidget.ColumnsCount + j);
			}
		}
		this.LoadDataAndSetupUI();
		this.m_inventorySlotWidget.CustomViewMatrix = new Matrix?(Matrix.CreateLookAt(new Vector3(0.7f, 0.25f, 1f), new Vector3(0f, -0.1f, 0f), Vector3.UnitY));
	}

	// Token: 0x06000002 RID: 2 RVA: 0x00002314 File Offset: 0x00000514
	private void LoadDataAndSetupUI()
	{
		this.m_speedSlider.MinValue = 1f;
		this.m_speedSlider.MaxValue = 3f;
		this.m_speedSlider.Granularity = 1f;
		this.m_rateSlider.MinValue = 1f;
		this.m_rateSlider.MaxValue = 15f;
		this.m_rateSlider.Granularity = 1f;
		this.m_spreadSlider.MinValue = 1f;
		this.m_spreadSlider.MaxValue = 3f;
		this.m_spreadSlider.Granularity = 1f;
		int slotValue = this.m_inventory.GetSlotValue(this.m_slotIndex);
		int data = Terrain.ExtractData(slotValue);
		int speedLevel = ItemsLauncherBlock.GetSpeedLevel(data);
		int rateLevel = ItemsLauncherBlock.GetRateLevel(data);
		int spreadLevel = ItemsLauncherBlock.GetSpreadLevel(data);
		this.m_speedSlider.Value = (float)((speedLevel == 0) ? 2 : speedLevel);
		this.m_rateSlider.Value = (float)((rateLevel == 0) ? 2 : rateLevel);
		this.m_spreadSlider.Value = (float)((spreadLevel == 0) ? 2 : spreadLevel);
		this.m_lastFuelCount = this.m_fuelInventory.GetSlotCount(0);
		int fuel = ItemsLauncherBlock.GetFuel(data);
		bool flag = fuel > 0;
		bool flag2 = flag;
		if (flag2)
		{
			this.m_fuelInventory.AddSlotItems(0, Terrain.MakeBlockValue(109), fuel);
		}
		this.UpdateTextBoxesFromSliders();
	}

	// Token: 0x06000003 RID: 3 RVA: 0x00002470 File Offset: 0x00000670
	public override void Update()
	{
		bool isSliding = this.m_speedSlider.IsSliding;
		bool flag5 = isSliding;
		if (flag5)
		{
			this.m_speedTextBox.Text = this.m_speedSlider.Value.ToString("0.##");
		}
		bool isSliding2 = this.m_rateSlider.IsSliding;
		bool flag6 = isSliding2;
		if (flag6)
		{
			this.m_rateTextBox.Text = this.m_rateSlider.Value.ToString("0.##");
		}
		bool isSliding3 = this.m_spreadSlider.IsSliding;
		bool flag7 = isSliding3;
		if (flag7)
		{
			this.m_spreadTextBox.Text = this.m_spreadSlider.Value.ToString("0.##");
		}
		bool hasFocus = this.m_speedTextBox.HasFocus;
		bool flag8 = hasFocus;
		if (flag8)
		{
			float num;
			bool flag = float.TryParse(this.m_speedTextBox.Text, out num);
			bool flag9 = flag;
			if (flag9)
			{
				this.m_speedSlider.Value = MathUtils.Clamp(num, this.m_speedSlider.MinValue, this.m_speedSlider.MaxValue);
			}
		}
		bool hasFocus2 = this.m_rateTextBox.HasFocus;
		bool flag10 = hasFocus2;
		if (flag10)
		{
			float num2;
			bool flag2 = float.TryParse(this.m_rateTextBox.Text, out num2);
			bool flag11 = flag2;
			if (flag11)
			{
				this.m_rateSlider.Value = MathUtils.Clamp(num2, this.m_rateSlider.MinValue, this.m_rateSlider.MaxValue);
			}
		}
		bool hasFocus3 = this.m_spreadTextBox.HasFocus;
		bool flag12 = hasFocus3;
		if (flag12)
		{
			float num3;
			bool flag3 = float.TryParse(this.m_spreadTextBox.Text, out num3);
			bool flag13 = flag3;
			if (flag13)
			{
				this.m_spreadSlider.Value = MathUtils.Clamp(num3, this.m_spreadSlider.MinValue, this.m_spreadSlider.MaxValue);
			}
		}
		int slotCount = this.m_fuelInventory.GetSlotCount(0);
		bool flag4 = slotCount != this.m_lastFuelCount;
		bool flag14 = flag4;
		if (flag14)
		{
			this.SaveFuelSetting();
			this.m_lastFuelCount = slotCount;
		}
		bool isClicked = this.m_cancelButton.IsClicked;
		bool flag15 = isClicked;
		if (flag15)
		{
			base.ParentWidget.Children.Remove(this);
		}
		bool isClicked2 = this.m_okButton.IsClicked;
		bool flag16 = isClicked2;
		if (flag16)
		{
			this.SaveSettings();
			base.ParentWidget.Children.Remove(this);
		}
		bool isClicked3 = this.m_helpButton.IsClicked;
		bool flag17 = isClicked3;
		if (flag17)
		{
			DialogsManager.ShowDialog(this.m_componentPlayer.GuiWidget, new MessageDialog("Use Auto-Cannon", "Authors\nFire Dragon\nKike\n----------\nThis weapon uses the first slot of the hotbar or the first available slot with an object. To save any changes, you must press 'Accept', or 'X' to discard. The spread and velocity settings determine the direction and distance of the projectiles.\n\nTo use it, simply hold right click if you are on PC, or hold the screen if you are on a mobile device, and it will automatically fire the times per second you have configured. Useful for shooting spawn eggs and generating creatures in large quantities in a short time.\n\nYou can also have fun launching fireworks or any other object.\n\n\nShitty code stolen by Samuelsan03, so do whatever you want with this code xd", "OK", null, null));
		}
	}

	// Token: 0x06000004 RID: 4 RVA: 0x00002714 File Offset: 0x00000914
	private void SaveFuelSetting()
	{
		int slotValue = this.m_inventory.GetSlotValue(this.m_slotIndex);
		int num = Terrain.ExtractData(slotValue);
		num = ItemsLauncherBlock.SetFuel(num, this.m_fuelInventory.GetSlotCount(0));
		int num2 = Terrain.ReplaceData(slotValue, num);
		this.m_inventory.RemoveSlotItems(this.m_slotIndex, 1);
		this.m_inventory.AddSlotItems(this.m_slotIndex, num2, 1);
	}

	// Token: 0x06000005 RID: 5 RVA: 0x00002780 File Offset: 0x00000980
	private void SaveSettings()
	{
		int speed = (int)this.m_speedSlider.Value;
		int rate = (int)this.m_rateSlider.Value;
		int spread = (int)this.m_spreadSlider.Value;
		int slotValue = this.m_inventory.GetSlotValue(this.m_slotIndex);
		int num = Terrain.ExtractData(slotValue);
		num = ItemsLauncherBlock.SetAllLevels(num, speed, rate, spread);
		int num2 = Terrain.ReplaceData(slotValue, num);
		this.m_inventory.RemoveSlotItems(this.m_slotIndex, 1);
		this.m_inventory.AddSlotItems(this.m_slotIndex, num2, 1);
	}

	// Token: 0x06000006 RID: 6 RVA: 0x00002810 File Offset: 0x00000A10
	private void UpdateTextBoxesFromSliders()
	{
		this.m_speedTextBox.Text = ((int)this.m_speedSlider.Value).ToString();
		this.m_rateTextBox.Text = ((int)this.m_rateSlider.Value).ToString();
		this.m_spreadTextBox.Text = ((int)this.m_spreadSlider.Value).ToString();
	}

	// Token: 0x04000001 RID: 1
	private readonly ComponentPlayer m_componentPlayer;

	// Token: 0x04000002 RID: 2
	private readonly IInventory m_inventory;

	// Token: 0x04000003 RID: 3
	private readonly int m_slotIndex;

	// Token: 0x04000004 RID: 4
	private readonly SliderWidget m_speedSlider;

	// Token: 0x04000005 RID: 5
	private readonly SliderWidget m_rateSlider;

	// Token: 0x04000006 RID: 6
	private readonly SliderWidget m_spreadSlider;

	// Token: 0x04000007 RID: 7
	private readonly TextBoxWidget m_speedTextBox;

	// Token: 0x04000008 RID: 8
	private readonly TextBoxWidget m_rateTextBox;

	// Token: 0x04000009 RID: 9
	private readonly TextBoxWidget m_spreadTextBox;

	// Token: 0x0400000A RID: 10
	private readonly ButtonWidget m_okButton;

	// Token: 0x0400000B RID: 11
	private readonly ButtonWidget m_helpButton;

	// Token: 0x0400000C RID: 12
	private readonly ButtonWidget m_cancelButton;

	// Token: 0x0400000D RID: 13
	private readonly InventorySlotWidget m_inventorySlotWidget;

	// Token: 0x0400000E RID: 14
	private readonly InventorySlotWidget m_fuelSlot;

	// Token: 0x0400000F RID: 15
	private readonly AutoCannonWidget.FuelInventory m_fuelInventory;

	// Token: 0x04000010 RID: 16
	private int m_lastFuelCount;

	// Token: 0x0200000D RID: 13
	[Nullable(0)]
	private class FuelInventory : IInventory
	{
		// Token: 0x17000018 RID: 24
		// (get) Token: 0x06000086 RID: 134 RVA: 0x0000808E File Offset: 0x0000628E
		// (set) Token: 0x06000087 RID: 135 RVA: 0x00008096 File Offset: 0x00006296
		public int VisibleSlotsCount { get; set; } = 1;

		// Token: 0x17000019 RID: 25
		// (get) Token: 0x06000088 RID: 136 RVA: 0x000080A0 File Offset: 0x000062A0
		public int SlotsCount
		{
			get
			{
				return 1;
			}
		}

		// Token: 0x1700001A RID: 26
		// (get) Token: 0x06000089 RID: 137 RVA: 0x000080B3 File Offset: 0x000062B3
		// (set) Token: 0x0600008A RID: 138 RVA: 0x000080BB File Offset: 0x000062BB
		public int ActiveSlotIndex { get; set; } = -1;

		// Token: 0x1700001B RID: 27
		// (get) Token: 0x0600008B RID: 139 RVA: 0x000080C4 File Offset: 0x000062C4
		public Project Project { get; }

		// Token: 0x0600008C RID: 140 RVA: 0x000080CC File Offset: 0x000062CC
		public FuelInventory(Project project)
		{
			this.Project = project;
		}

		// Token: 0x0600008D RID: 141 RVA: 0x000080EC File Offset: 0x000062EC
		public int GetSlotValue(int slotIndex)
		{
			return this.m_fuelValue;
		}

		// Token: 0x0600008E RID: 142 RVA: 0x00008104 File Offset: 0x00006304
		public int GetSlotCount(int slotIndex)
		{
			return this.m_fuelCount;
		}

		// Token: 0x0600008F RID: 143 RVA: 0x0000811C File Offset: 0x0000631C
		public int GetSlotCapacity(int slotIndex, int value)
		{
			return (Terrain.ExtractContents(value) == 109) ? 40 : 0;
		}

		// Token: 0x06000090 RID: 144 RVA: 0x00008140 File Offset: 0x00006340
		public void AddSlotItems(int slotIndex, int value, int count)
		{
			bool flag = slotIndex == 0;
			bool flag2 = flag;
			if (flag2)
			{
				this.m_fuelValue = value;
				this.m_fuelCount += count;
			}
		}

		// Token: 0x06000091 RID: 145 RVA: 0x00008170 File Offset: 0x00006370
		public int RemoveSlotItems(int slotIndex, int count)
		{
			bool flag = slotIndex == 0;
			bool flag3 = flag;
			int result;
			if (flag3)
			{
				int num = MathUtils.Min(count, this.m_fuelCount);
				this.m_fuelCount -= num;
				bool flag2 = this.m_fuelCount == 0;
				bool flag4 = flag2;
				if (flag4)
				{
					this.m_fuelValue = 0;
				}
				result = num;
			}
			else
			{
				result = 0;
			}
			return result;
		}

		// Token: 0x06000092 RID: 146 RVA: 0x000081D0 File Offset: 0x000063D0
		public void DropAllItems(Vector3 position)
		{
		}

		// Token: 0x06000093 RID: 147 RVA: 0x000081D4 File Offset: 0x000063D4
		public int GetSlotProcessCapacity(int slotIndex, int value)
		{
			return 0;
		}

		// Token: 0x06000094 RID: 148 RVA: 0x000081E7 File Offset: 0x000063E7
		public void ProcessSlotItems(int slotIndex, int value, int count, int processCount, out int resultValue, out int resultCount)
		{
			resultValue = 0;
			resultCount = 0;
		}

		// Token: 0x0400009A RID: 154
		public int m_fuelValue;

		// Token: 0x0400009B RID: 155
		public int m_fuelCount;
	}
}
