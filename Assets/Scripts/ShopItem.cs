using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ShopItem : MonoBehaviour
{
    private TextMeshProUGUI m_NameText;
    private TextMeshProUGUI m_PriceText;
    private Image m_Image;

    private Button m_Button;

    [HideInInspector]
    public ShopManager m_ShopManager;

    public int m_ItemID { get; set; }
    public string m_Name { get; set; }
    public string m_Desctiption { get; set; }
    public int m_Price { get; set; }
    public Sprite m_Sprite { get; set; }
    public int m_Stock { get; set; }

    private void Awake()
    {
        m_NameText = transform.GetChild(0).GetComponent<TextMeshProUGUI>();
        m_PriceText = transform.GetChild(1).GetComponent<TextMeshProUGUI>();
        m_Image = transform.GetChild(2).GetComponent<Image>();

        m_Button = GetComponent<Button>();
    }

    public void InitializeShopItem(int itemID, string name, string description, int price, Sprite image, int stock)
    {
        m_ItemID = itemID;
        m_Name = name;
        m_Desctiption = description;
        m_Price = price;
        m_Sprite = image;
        m_Stock = stock;

        m_NameText.text = name;
        m_PriceText.text = price + " €";
        m_Image.sprite = image;

        m_Button.onClick.AddListener(() => m_ShopManager.ShowItemFullscreen(this));
    }
}
