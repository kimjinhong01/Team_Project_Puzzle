using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using DG.Tweening;

public class UIInventory : MonoBehaviour
{
    [SerializeField] private ItemSlot[] slots;

    [SerializeField] private Transform slotPanel;
    [SerializeField] private Transform dropPosition;

    [Header("Selected Item")]
    [SerializeField] private TextMeshProUGUI selectedItemText;
    [SerializeField] private TextMeshProUGUI selectedItemTextPlus;

    private CanvasGroup selectedItemTextCanvas;
    private ItemSlot selectedItem;
    private int selectedItemIndex;
    // private bool firstScrollSkipped;
    private bool canUse;
    private bool canEquip;
    private bool canUnEquip;
    private bool canDrop;

    private int curEquipIndex;

    private bool isTextVisible = false;

    private PlayerCondition condition;

    public ItemData ItemData => selectedItem.Item;

    private void Start()
    {
        condition = CharacterManager.Instance.Player.Condition;
        dropPosition = CharacterManager.Instance.Player.DropPosition;

        CharacterManager.Instance.Player.AddItem += AddItem;
        CharacterManager.Instance.Player.Controller.Inventory = this;

        slots = new ItemSlot[slotPanel.childCount - 1];

        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = slotPanel.GetChild(i + 1).GetComponent<ItemSlot>();
            slots[i].Index = i;
            slots[i].IndexText.text = (i + 1).ToString();
            slots[i].Inventory = this;
            slots[i].Clear();
        }

        ClearSelectedItemWindow();

        selectedItemTextCanvas = selectedItemText.GetComponent<CanvasGroup>();
        selectedItemTextCanvas.alpha = 0f;

        selectedItemText.text = string.Empty;
        selectedItemTextPlus.text = string.Empty;
    }

    private void Update()
    {
        for (int i = 0; i < Mathf.Min(slots.Length, 10); i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                selectedItemIndex = i;
            }
        }

        SelectItem(selectedItemIndex);

        bool shouldShow = selectedItemText.text != string.Empty;
        if (isTextVisible != shouldShow)
        {
            isTextVisible = shouldShow;
            AnimateSelectedItemText(shouldShow);
        }
    }

    private void AnimateSelectedItemText(bool show)
    {
        float alpha = show ? 1f : 0f;
        float yPos = show ? 50f : 25f;

        selectedItemTextCanvas.DOFade(alpha, 0.5f)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true);
    }

    private void ClearSelectedItemWindow()
    {
        selectedItem = null;

        selectedItemText.text = string.Empty;

        canUse = false;
        canEquip = false;
        canUnEquip = false;
        canDrop = false;
    }

    public void AddItem()
    {
        ItemData data = CharacterManager.Instance.Player.ItemData;

        if (data.canStack)
        {
            ItemSlot slot = GetItemStack(data);
            if (slot != null)
            {
                slot.Quantity++;
                UpdateUI();
                CharacterManager.Instance.Player.ItemData = null;
                return;
            }
        }

        ItemSlot emptySlot = GetEmptySlot();

        if (emptySlot != null)
        {
            emptySlot.Item = data;
            emptySlot.Quantity = 1;
            UpdateUI();
            CharacterManager.Instance.Player.ItemData = null;
            return;
        }

        ThrowItem(data);
        CharacterManager.Instance.Player.ItemData = null;
    }

    public void UpdateUI()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].Item != null)
            {
                slots[i].Set();
            }
            else
            {
                slots[i].Clear();
            }
        }
    }

    private ItemSlot GetItemStack(ItemData data)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].Item == data && slots[i].Quantity < data.maxStackAmount)
            {
                return slots[i];
            }
        }
        return null;
    }

    private ItemSlot GetEmptySlot()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].Item == null)
            {
                return slots[i];
            }
        }
        return null;
    }

    // Player 스크립트 먼저 수정
    public void ThrowItem(ItemData data)
    {
        Instantiate(data.dropPrefab, dropPosition.position, Quaternion.Euler(360f * Random.value * Vector3.one));
    }


    // ItemSlot 스크립트 먼저 수정
    public void SelectItem(int index)
    {
        // if (slots[index] == selectedItem && EventSystem.current.currentSelectedGameObject != null) return;

        selectedItem = slots[index];
        selectedItemIndex = index;

        selectedItem.Button.Select();

        if (slots[index].Item == null)
        {
            selectedItemText.text = string.Empty;
            
            canUse = false;
            canEquip = false;
            canUnEquip = false;
            canDrop = false;
            return;
        }

        selectedItemText.text = $"<font=\"GmarketSansMedium SDF\" material=\"GmarketSansMedium SDF Glow Blue\">{selectedItem.Item.displayName}</font> - {selectedItem.Item.description}";
        selectedItemTextPlus.text = selectedItemText.text;

        canUse = selectedItem.Item.type == ItemType.Consumable;
        canEquip = selectedItem.Item.type == ItemType.Equipable && !slots[index].Equipped;
        canUnEquip = selectedItem.Item.type == ItemType.Equipable && slots[index].Equipped;
        canDrop = true;
    }


#region Input

    /// <summary>
    /// 아이템 선택 [마우스 휠]
    /// </summary>
    public void Scroll()
    {
        selectedItemIndex = 1 - selectedItemIndex;
    }

    /// <summary>
    /// 아이템 버리기 [Q]
    /// </summary>
    /// <param name="context"></param>
    public void OnDropInput(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed && canDrop)
        {
            ThrowItem(selectedItem.Item);
            RemoveSelctedItem();
        }
    }

    /// <summary>
    /// 아이템 사용 [F]
    /// </summary>
    /// <param name="context"></param>
    public void OnUseInput(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
        {
            // 소비 아이템 -> 사용
            if (canUse)
            {
                for (int i = 0; i < selectedItem.Item.consumables.Length; i++)
                {
                    switch (selectedItem.Item.consumables[i].type)
                    {
                        case ConsumableType.Health:
                            condition.Heal(selectedItem.Item.consumables[i].value);
                            break;
                        case ConsumableType.SpeedBoost:
                            condition.ApplySpeedBoost(selectedItem.Item.consumables[i].value, selectedItem.Item.consumables[i].duration);
                            break;
                    }
                }

                RemoveSelctedItem();
            }

            // 장비 아이템 -> 장착
            if (canEquip)
            {
                if (slots[curEquipIndex].Equipped)
                {
                    UnEquip(curEquipIndex);
                }

                slots[selectedItemIndex].Equipped = true;
                curEquipIndex = selectedItemIndex;
                CharacterManager.Instance.Player.Equipment.EquipNew(selectedItem.Item);
                UpdateUI();

                // SelectItem(selectedItemIndex);
            }

            // 장비 아이템 -> 장착해제
            if (canUnEquip)
            {
                UnEquip(selectedItemIndex);
            }
        }
    }
    
#endregion


    private void RemoveSelctedItem()
    {
        selectedItem.Quantity--;

        if (selectedItem.Quantity <= 0)
        {
            if (slots[selectedItemIndex].Equipped)
            {
                UnEquip(selectedItemIndex);
            }

            selectedItem.Item = null;
            ClearSelectedItemWindow();
        }

        UpdateUI();
    }

    private void UnEquip(int index)
    {
        slots[index].Equipped = false;
        CharacterManager.Instance.Player.Equipment.UnEquip();
        UpdateUI();

        // if (selectedItemIndex == index)
        // {
        //     SelectItem(selectedItemIndex);
        // }
    }

    public bool HasItem(ItemData item, int quantity)
    {
        return false;
    }
}