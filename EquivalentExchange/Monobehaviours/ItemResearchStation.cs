﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EquivalentExchange.Monobehaviours
{
	using HarmonyLib;
	using Nautilus.Handlers;
	using System;
	using System.Collections.Generic;
	using System.Reflection;
	using UnityEngine;

	// Token: 0x02000265 RID: 613
	public class ItemResearchStation : MonoBehaviour
	{
		// Token: 0x0600118C RID: 4492 RVA: 0x0005EAF8 File Offset: 0x0005CCF8
		public void Awake()
        {
			OnEnable();
        }
		private void OnEnable()
		{
			if(storageContainer == null)
            {
				storageContainer = GetComponent<StorageContainer>();
            }
			if (TryGetComponent(out Trashcan tc))
			{
				storageContainer.container.onAddItem -= tc.AddItem;
				storageContainer.container.onRemoveItem -= tc.RemoveItem;
				storageContainer.container.isAllowedToAdd = null;
				Destroy(tc);
			}
			if (!this.subscribed)
			{
				this.storageContainer.enabled = true;
				this.storageContainer.container.onAddItem += this.AddItem;
				this.storageContainer.container.onRemoveItem += this.RemoveItem;
				this.subscribed = true;
				storageContainer.hoverText = "Use Item Research Station";
				storageContainer.storageLabel = "Item Research Station";
				storageContainer.container._label = "Item Research Station";
            }
            storageContainer.container.isAllowedToAdd = new IsAllowedToAdd(IsAllowedToAdd);
        }

		// Token: 0x0600118D RID: 4493 RVA: 0x0005EB88 File Offset: 0x0005CD88
		private void OnDisable()
		{
			if (this.subscribed)
			{
				this.storageContainer.container.onAddItem -= this.AddItem;
				this.storageContainer.container.onRemoveItem -= this.RemoveItem;
				this.storageContainer.container.isAllowedToAdd = null;
				this.subscribed = false;
				this.storageContainer.enabled = false;
			}
		}

		// Token: 0x0600118E RID: 4494 RVA: 0x0005EBFC File Offset: 0x0005CDFC
		private void Update()
		{
			if (this.wasteList.Count > 0 && this.timeLastWasteDestroyed + (double)this.destroyInterval < DayNightCycle.main.timePassed)
			{
				Trashcan.Waste waste = this.wasteList[0];
				if (ItemDragManager.isDragging && waste.inventoryItem == ItemDragManager.draggedItem)
				{
					waste = ((this.wasteList.Count > 1) ? this.wasteList[1] : null);
				}
				if (waste != null && waste.timeAdded + (double)this.startDestroyTimeOut < DayNightCycle.main.timePassed)
				{
					this.timeLastWasteDestroyed = DayNightCycle.main.timePassed;
					Pickupable item = waste.inventoryItem.item;
					if (this.storageContainer.container.RemoveItem(item, true))
					{
						float cost = ExchangeMenu.GetCost(item.GetTechType(), 0, false, false);

						var battery = item.GetComponentInChildren<Battery>();
						if(battery != null)
						{
							TechType batteryType;
							if (battery.TryGetComponent<Pickupable>(out var pickup))
								batteryType = pickup.GetTechType();
							else
								batteryType = CraftData.GetTechType(battery.gameObject);

							var chargePercent = battery.charge / battery.capacity;
							cost -= (int)(ExchangeMenu.GetCost(batteryType, 0, false, false) * chargePercent);
						}

                        if (QMod.TryUnlockTechType(item.GetTechType(), out string reason))
						{
							if(QMod.config.researchStationMessages)
								ErrorMessage.AddMessage($"Unlocked {item.GetTechType()}, gained {cost} ECM");
						}
						else
						{
                            if (QMod.config.researchStationMessages)
                                ErrorMessage.AddMessage($"Could not unlock {item.GetTechType()} due to: {reason}, still gained {cost} ECM");
						}

						QMod.SaveData.ECMAvailable += cost;
						UnityEngine.Object.Destroy(item.gameObject);
					}
				}
			}
		}

		// Token: 0x0600118F RID: 4495 RVA: 0x0005ECD0 File Offset: 0x0005CED0
		private bool IsAllowedToAdd(Pickupable pickupable, bool verbose)
		{
			if (!QMod.config.researchStationFCSFilters) return true;


			TechType techType = pickupable.GetTechType();

			if (techType.ToString().ToLower() == "debitcard") return true;
			/*
			 * FCS Compat. Re-add/implement once FCS mods are updated
			if(((Dictionary<TechType, Assembly>)AccessTools.Field(typeof(TechTypeHandler), "TechTypesAddedBy").GetValue(null)).TryGetValue(techType, out var assembly))
			{
				if(assembly.GetName().Name.Contains("FCS_"))
				{
					ErrorMessage.AddMessage("Can't add FCS items to the item research station. Please return them through the FCS pda instead");
					return false;
				}
            }*/
            return true;
        }

		// Token: 0x06001190 RID: 4496 RVA: 0x0005ED24 File Offset: 0x0005CF24
		private void AddItem(InventoryItem item)
		{
			this.wasteList.Add(new Trashcan.Waste(item, DayNightCycle.main.timePassed));
		}

		// Token: 0x06001191 RID: 4497 RVA: 0x0005ED44 File Offset: 0x0005CF44
		private void RemoveItem(InventoryItem item)
		{
			for (int i = 0; i < this.wasteList.Count; i++)
			{
				if (this.wasteList[i].inventoryItem == item)
				{
					this.wasteList.RemoveAt(i);
					return;
				}
			}
		}

		// Token: 0x04001320 RID: 4896
		[AssertNotNull]
		public StorageContainer storageContainer;

		// Token: 0x04001321 RID: 4897
		public float startDestroyTimeOut => storageContainer?.container?.count >= 4 ? 1f : 5f;

		// Token: 0x04001322 RID: 4898
		public float destroyInterval = 1f;

		// Token: 0x04001325 RID: 4901
		private bool subscribed;

		// Token: 0x04001326 RID: 4902
		private List<Trashcan.Waste> wasteList = new List<Trashcan.Waste>();

		// Token: 0x04001327 RID: 4903
		private double timeLastWasteDestroyed;

		// Token: 0x02000818 RID: 2072
		private class Waste
		{
			// Token: 0x06004327 RID: 17191 RVA: 0x0016553F File Offset: 0x0016373F
			public Waste(InventoryItem inventoryItem, double timeAdded)
			{
				this.inventoryItem = inventoryItem;
				this.timeAdded = timeAdded;
			}

			// Token: 0x04003BF3 RID: 15347
			public InventoryItem inventoryItem;

			// Token: 0x04003BF4 RID: 15348
			public double timeAdded;
		}
	}

}
