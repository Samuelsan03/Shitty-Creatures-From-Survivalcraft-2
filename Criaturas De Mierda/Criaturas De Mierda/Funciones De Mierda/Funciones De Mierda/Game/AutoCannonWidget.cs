using System;
using System.Xml.Linq;
using Engine;
using Game;
using GameEntitySystem;

// Token: 0x02000002 RID: 2
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
		if (flag)
		{
			this.m_fuelInventory.AddSlotItems(0, Terrain.MakeBlockValue(109), fuel);
		}
		this.UpdateTextBoxesFromSliders();
	}

	// Token: 0x06000003 RID: 3 RVA: 0x0000246C File Offset: 0x0000066C
	public override void Update()
	{
		bool isSliding = this.m_speedSlider.IsSliding;
		if (isSliding)
		{
			this.m_speedTextBox.Text = this.m_speedSlider.Value.ToString("0.##");
		}
		bool isSliding2 = this.m_rateSlider.IsSliding;
		if (isSliding2)
		{
			this.m_rateTextBox.Text = this.m_rateSlider.Value.ToString("0.##");
		}
		bool isSliding3 = this.m_spreadSlider.IsSliding;
		if (isSliding3)
		{
			this.m_spreadTextBox.Text = this.m_spreadSlider.Value.ToString("0.##");
		}
		bool hasFocus = this.m_speedTextBox.HasFocus;
		if (hasFocus)
		{
			float num;
			bool flag = float.TryParse(this.m_speedTextBox.Text, out num);
			if (flag)
			{
				this.m_speedSlider.Value = MathUtils.Clamp(num, this.m_speedSlider.MinValue, this.m_speedSlider.MaxValue);
			}
		}
		bool hasFocus2 = this.m_rateTextBox.HasFocus;
		if (hasFocus2)
		{
			float num2;
			bool flag2 = float.TryParse(this.m_rateTextBox.Text, out num2);
			if (flag2)
			{
				this.m_rateSlider.Value = MathUtils.Clamp(num2, this.m_rateSlider.MinValue, this.m_rateSlider.MaxValue);
			}
		}
		bool hasFocus3 = this.m_spreadTextBox.HasFocus;
		if (hasFocus3)
		{
			float num3;
			bool flag3 = float.TryParse(this.m_spreadTextBox.Text, out num3);
			if (flag3)
			{
				this.m_spreadSlider.Value = MathUtils.Clamp(num3, this.m_spreadSlider.MinValue, this.m_spreadSlider.MaxValue);
			}
		}
		int slotCount = this.m_fuelInventory.GetSlotCount(0);
		bool flag4 = slotCount != this.m_lastFuelCount;
		if (flag4)
		{
			this.SaveFuelSetting();
			this.m_lastFuelCount = slotCount;
		}
		bool isClicked = this.m_cancelButton.IsClicked;
		if (isClicked)
		{
			base.ParentWidget.Children.Remove(this);
		}
		bool isClicked2 = this.m_okButton.IsClicked;
		if (isClicked2)
		{
			this.SaveSettings();
			base.ParentWidget.Children.Remove(this);
		}
		bool isClicked3 = this.m_helpButton.IsClicked;
		if (isClicked3)
		{
			DialogsManager.ShowDialog(this.m_componentPlayer.GuiWidget, new MessageDialog("Utilizar Auto-Cañón", "Autores\nFire Dragon\nKike\n----------\nEsta arma usa la primera casilla del inventario en mano o la que tenga un objeto disponible, para guardar todo cambio, debes presionar ''Aceptar'', o ''X'' para descartar. Los ajustes de dispersión y velocidad determinan la dirección y la distancia de los proyectiles.\n\nPara usarla, simplemente mantén presionado clic derecho si es el caso de PC, o la pantalla si es el caso de un dispositivo móvil, y ella sola disparara las veces por segundo que hallas configurado, útil para disparar huevos generadores y generar criaturas en gran cantidad en poco tiempo.\n\nTambien puedes divertirte lanzando fuegos artificiales o cualquier otro objeto.", "OK", null, null));
		}
	}

	// Token: 0x06000004 RID: 4 RVA: 0x000026D8 File Offset: 0x000008D8
	private void SaveFuelSetting()
	{
		int slotValue = this.m_inventory.GetSlotValue(this.m_slotIndex);
		int num = Terrain.ExtractData(slotValue);
		num = ItemsLauncherBlock.SetFuel(num, this.m_fuelInventory.GetSlotCount(0));
		int num2 = Terrain.ReplaceData(slotValue, num);
		this.m_inventory.RemoveSlotItems(this.m_slotIndex, 1);
		this.m_inventory.AddSlotItems(this.m_slotIndex, num2, 1);
	}

	// Token: 0x06000005 RID: 5 RVA: 0x00002744 File Offset: 0x00000944
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

	// Token: 0x06000006 RID: 6 RVA: 0x000027D4 File Offset: 0x000009D4
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

	// Token: 0x02000007 RID: 7
	private class FuelInventory : IInventory
	{
		// Token: 0x17000002 RID: 2
		// (get) Token: 0x0600001F RID: 31 RVA: 0x00003114 File Offset: 0x00001314
		// (set) Token: 0x06000020 RID: 32 RVA: 0x0000311C File Offset: 0x0000131C
		public int VisibleSlotsCount { get; set; } = 1;

		// Token: 0x17000003 RID: 3
		// (get) Token: 0x06000021 RID: 33 RVA: 0x00003125 File Offset: 0x00001325
		public int SlotsCount
		{
			get
			{
				return 1;
			}
		}

		// Token: 0x17000004 RID: 4
		// (get) Token: 0x06000022 RID: 34 RVA: 0x00003128 File Offset: 0x00001328
		// (set) Token: 0x06000023 RID: 35 RVA: 0x00003130 File Offset: 0x00001330
		public int ActiveSlotIndex { get; set; } = -1;

		// Token: 0x17000005 RID: 5
		// (get) Token: 0x06000024 RID: 36 RVA: 0x00003139 File Offset: 0x00001339
		public Project Project { get; }

		// Token: 0x06000025 RID: 37 RVA: 0x00003141 File Offset: 0x00001341
		public FuelInventory(Project project)
		{
			this.Project = project;
		}

		// Token: 0x06000026 RID: 38 RVA: 0x00003160 File Offset: 0x00001360
		public int GetSlotValue(int slotIndex)
		{
			return this.m_fuelValue;
		}

		// Token: 0x06000027 RID: 39 RVA: 0x00003168 File Offset: 0x00001368
		public int GetSlotCount(int slotIndex)
		{
			return this.m_fuelCount;
		}

		// Token: 0x06000028 RID: 40 RVA: 0x00003170 File Offset: 0x00001370
		public int GetSlotCapacity(int slotIndex, int value)
		{
			return (Terrain.ExtractContents(value) == 109) ? 40 : 0;
		}

		// Token: 0x06000029 RID: 41 RVA: 0x00003184 File Offset: 0x00001384
		public void AddSlotItems(int slotIndex, int value, int count)
		{
			bool flag = slotIndex == 0;
			if (flag)
			{
				this.m_fuelValue = value;
				this.m_fuelCount += count;
			}
		}

		// Token: 0x0600002A RID: 42 RVA: 0x000031B4 File Offset: 0x000013B4
		public int RemoveSlotItems(int slotIndex, int count)
		{
			bool flag = slotIndex == 0;
			int result;
			if (flag)
			{
				int num = MathUtils.Min(count, this.m_fuelCount);
				this.m_fuelCount -= num;
				bool flag2 = this.m_fuelCount == 0;
				if (flag2)
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

		// Token: 0x0600002B RID: 43 RVA: 0x00003203 File Offset: 0x00001403
		public void DropAllItems(Vector3 position)
		{
		}

		// Token: 0x0600002C RID: 44 RVA: 0x00003206 File Offset: 0x00001406
		public int GetSlotProcessCapacity(int slotIndex, int value)
		{
			return 0;
		}

		// Token: 0x0600002D RID: 45 RVA: 0x00003209 File Offset: 0x00001409
		public void ProcessSlotItems(int slotIndex, int value, int count, int processCount, out int resultValue, out int resultCount)
		{
			resultValue = 0;
			resultCount = 0;
		}

		// Token: 0x04000025 RID: 37
		public int m_fuelValue;

		// Token: 0x04000026 RID: 38
		public int m_fuelCount;
	}
}
