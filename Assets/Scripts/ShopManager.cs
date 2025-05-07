using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Newtonsoft.Json;
using UnityEngine.SceneManagement;
using System.Linq;
using System;
using System.Threading;
using System.Threading.Tasks;

public class ShopManager : MonoBehaviour
{
    [SerializeField]
    private TMP_Dropdown m_CategoryDropdown;
    [SerializeField]
    private List<Toggle> m_FilterToggles;

    [SerializeField]
    private RectTransform m_ShopItemsParent;
    [SerializeField]
    private GameObject m_ShopItemPrefab;

    private Queue<ShopItem> m_ActiveShopItems = new Queue<ShopItem>();
    private Queue<ShopItem> m_InactiveShopItems = new Queue<ShopItem>();
    [SerializeField]
    private int m_InitialShopItemNumber = 30;

    [SerializeField]
    private GameObject m_FullscreenItemPanel;
    [SerializeField]
    private TextMeshProUGUI m_ItemNameFS;
    [SerializeField]
    private Image m_ItemImageFS;
    [SerializeField]
    private TextMeshProUGUI m_ItemDescriptionFS;
    [SerializeField]
    private TextMeshProUGUI m_ItemPriceFS;
    [SerializeField]
    private TextMeshProUGUI m_ItemStockFS;
    [SerializeField]
    private Button m_BuyItemButton;

    [SerializeField]
    private TextMeshProUGUI m_ResponseOutputText;

    private volatile Queue<ItemImage> m_ItemImagesToGenerate;

    private static Mutex mutex = new Mutex(false, "MyMutex");

    private Dictionary<int, Sprite> m_CachedItemImages;
    private readonly Dictionary<int, DateTime> m_CachedImageTimes = new Dictionary<int, DateTime>();

    private void Start()
    {
        m_ItemImagesToGenerate = new Queue<ItemImage>();
        m_CachedItemImages = new Dictionary<int, Sprite>();

        for (int i = 0; i < m_InitialShopItemNumber; i++)
        {
            ShopItem newItem = Instantiate(m_ShopItemPrefab, m_ShopItemsParent).GetComponent<ShopItem>();

            newItem.m_ShopManager = this;
            newItem.gameObject.SetActive(false);

            m_InactiveShopItems.Enqueue(newItem);
        }

        GetItems();
    }

    private void Update()
    {
        if (m_ItemImagesToGenerate.Count > 0)
        {
            mutex.WaitOne();

            ItemImage itemImage = m_ItemImagesToGenerate.Dequeue();

            if (itemImage != null)
            {
                Sprite sprite = GenerateSprite(itemImage.m_ImageBytes);
                itemImage.m_Item.SetSprite(sprite);

                m_CachedItemImages.Add(itemImage.m_Item.m_ItemID, sprite);

            }
            else
            {
                Debug.Log("The item image is not set!");
            }

            mutex.ReleaseMutex();
        }
    }

    public void GetItems()
    {
        while (m_ActiveShopItems.Count > 0)
        {
            ShopItem item = m_ActiveShopItems.Dequeue();
            item.gameObject.SetActive(false);

            m_InactiveShopItems.Enqueue(item);
        }

        string requestURL = "/items?";

        int category = m_CategoryDropdown.value;

        requestURL += $"category={category}";

        for (int i = 0; i < m_FilterToggles.Count; i++)
        {
            if (m_FilterToggles[i].isOn)
            {
                requestURL += $"&filters[]={i+1}";
            }
        }

        ParsedHTTPResponse response = MyHTTPClient.SendRequestToServer("GET", requestURL);

        m_ResponseOutputText.text = $"{response.m_StatusCode}: {response.m_StatusMessage}.";

        if (response.m_StatusCode == 202)
        {
            if (response.m_Headers["Content-Type"] == "application/json" && (int.TryParse(response.m_Headers["Content-Length"], out int i) ? i : 0) == response.m_Body.Length)
            {
                ShopItems itemList = JsonConvert.DeserializeObject<ShopItems>(response.m_Body);

                foreach (Dictionary<string, object> item in itemList.m_Items)
                {
                    if (m_InactiveShopItems.Count <= 0)
                        break;

                    ShopItem shopItem = m_InactiveShopItems.Dequeue();

                    shopItem.gameObject.SetActive(true);

                    int itemID = int.TryParse(item["itemID"].ToString(), out i) ? i : -1;
                    int itemPrice = int.TryParse(item["itemPrice"].ToString(), out i) ? i : -1;
                    int itemStock = int.TryParse(item["itemStock"].ToString(), out i) ? i : -1;

                    if (m_CachedItemImages.ContainsKey(itemID))
                    {
                        DateTime cachedTime = m_CachedImageTimes.ContainsKey(itemID) ? m_CachedImageTimes[itemID] : DateTime.MinValue;
                        ParsedHTTPResponse headResponse = MyHTTPClient.SendRequestToServer("HEAD", $"/image?itemID={itemID}");

                        if (headResponse.m_StatusCode == 304)
                        {
                            shopItem.SetSprite(m_CachedItemImages[itemID]);
                        }
                        else
                        {
                            Thread getItemImage = new Thread(RequestImage);
                            getItemImage.Start(shopItem);
                        }
                    }
                    else
                    {
                        Thread getItemImage = new Thread(RequestImage);
                        getItemImage.Start(shopItem);
                    }


                    shopItem.InitializeShopItem(itemID, item["itemName"].ToString(), item["itemDescription"].ToString(), itemPrice, itemStock);

                    m_ActiveShopItems.Enqueue(shopItem);
                }

                m_ShopItemsParent.sizeDelta = new Vector2(m_ShopItemsParent.sizeDelta.x, Mathf.CeilToInt(m_ActiveShopItems.Count / m_ShopItemsParent.GetComponent<GridLayoutGroup>().constraintCount) * m_ShopItemPrefab.GetComponent<RectTransform>().sizeDelta.y);
            }
        }
        else if (response.m_StatusCode == 401)
        {
            Debug.Log(response.m_Body);
        }
        else
        {
            Debug.Log(response.m_StatusMessage);
        }
    }

    public void ShowItemFullscreen(ShopItem item)
    {
        m_ItemNameFS.text = item.m_Name;
        m_ItemDescriptionFS.text = item.m_Description;
        m_ItemImageFS.sprite = item.m_Sprite;
        m_ItemPriceFS.text = $"{item.m_Price} €";

        if (item.m_Stock > 0)
        {
            m_ItemStockFS.text = $"There are only {item.m_Stock} units left!";
            m_BuyItemButton.onClick.RemoveAllListeners();
            m_BuyItemButton.onClick.AddListener(() => BuyItem(item));
            m_BuyItemButton.interactable = true;
        }
        else
        {
            m_ItemStockFS.text = "There are no units left!";
            m_BuyItemButton.onClick.RemoveAllListeners();
            m_BuyItemButton.interactable = false;
        }

        m_FullscreenItemPanel.SetActive(true);

        if (item.m_Sprite == null)
        {
            ParsedHTTPResponse response = MyHTTPClient.SendRequestToServer("GET", $"/image?itemID={item.m_ItemID}");

            m_ResponseOutputText.text = $"{response.m_StatusCode}: {response.m_StatusMessage}.";

            if (response.m_StatusCode == 200 && response.m_Headers["Content-Type"] == "image/png")
            {
                Sprite sprite = GenerateSprite(response.m_BodyBytes);
                item.SetSprite(sprite);

                m_CachedItemImages.Add(item.m_ItemID, sprite);
            }
        }
        else
        {
            if (m_CachedImageTimes.TryGetValue(item.m_ItemID, out DateTime cachedTime))
            {
                ParsedHTTPResponse headResponse = MyHTTPClient.SendRequestToServer(
                    "HEAD",
                    $"/image?itemID={item.m_ItemID}",
                    headers: $"If-Modified-Since: {cachedTime.ToString("r")}\r\n"
                );

                if (headResponse.m_StatusCode == 304)
                {
                    m_ItemImageFS.sprite = m_CachedItemImages[item.m_ItemID];
                }
                else
                {
                    RequestImage(item);
                    m_ItemImageFS.sprite = m_CachedItemImages[item.m_ItemID];
                }
            }
        }


        m_ItemImageFS.sprite = item.m_Sprite;

        if (m_ItemImageFS.sprite == null)
        {
            Debug.LogError("Sprite assignment failed!");
        }
    }

    private void RequestImage(object info)
    {
        ShopItem item = (ShopItem) info;


        if (item.m_ItemID == 0)
        {
            Debug.Log("The item id of this item is for some reason 0!");
        }

        Thread.Sleep(10);


        ParsedHTTPResponse headResponse = MyHTTPClient.SendRequestToServer("HEAD", $"/image?itemID={item.m_ItemID}");

        if (headResponse.m_StatusCode == 304)
        {
            item.SetSprite(m_CachedItemImages[item.m_ItemID]);
            return;
        }

        DateTime cachedTime = m_CachedImageTimes.ContainsKey(item.m_ItemID) ? m_CachedImageTimes[item.m_ItemID] : DateTime.MinValue;

        ParsedHTTPResponse response = MyHTTPClient.SendRequestToServer("GET",$"/image?itemID={item.m_ItemID}",headers: $"If-Modified-Since: {cachedTime.ToString("r")}\r\n");

        if (response.m_StatusCode == 200 && response.m_Headers["Content-Type"] == "image/png")
        {
            DateTime newTime = DateTime.Parse(response.m_Headers["Last-Modified"]);
            m_CachedImageTimes[item.m_ItemID] = newTime;
            //PlayerPrefs.SetString($"img_{item.m_ItemID}", newTime.ToString("r"));

            mutex.WaitOne();
            m_ItemImagesToGenerate.Enqueue(new ItemImage(item, response.m_BodyBytes));
            mutex.ReleaseMutex();
        }
    }

    public void CloseItemFullscreen()
    {
        m_FullscreenItemPanel.SetActive(false);
    }

    private void BuyItem(ShopItem item)
    {
        ParsedHTTPResponse response = MyHTTPClient.SendRequestToServer("PATCH", $"/buyItem?itemID={item.m_ItemID}");

        m_ResponseOutputText.text = $"{response.m_StatusCode}: {response.m_StatusMessage}.";

        if (response.m_StatusCode == 214)
        {
            if (response.m_Headers["Content-Type"] == "application/json" && (int.TryParse(response.m_Headers["Content-Length"], out int i) ? i : 0) == response.m_Body.Length)
            {
                ShopItems shopItemInfo = JsonConvert.DeserializeObject<ShopItems>(response.m_Body);

                item.m_Stock = int.TryParse(shopItemInfo.m_Items[0]["itemStock"].ToString(), out i) ? i : -1;

                ShowItemFullscreen(item);
            }
        }
    }

    public void Logout()
    {
        MyHTTPClient.SetUserID(-1);
        MyHTTPClient.SetAuthenticationKey(string.Empty);

        SceneManager.LoadScene("LoginScene");
    }

    public void DeleteAccount()
    {
        ParsedHTTPResponse response = MyHTTPClient.SendRequestToServer("DELETE", "/deleteAccount");

        if (response.m_StatusCode == 202)
        {
            Logout();
        }
    }

    private Sprite GenerateSprite(byte[] imageBytes)
    {
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

        if (!texture.LoadImage(imageBytes))
        {
            Debug.LogError("LoadImage failed: The byte array might be corrupt or not a valid image.");
            return null;
        }

        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.Apply();

        Sprite newSprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f), // Center pivot
            100f, // Pixels per unit
            0,
            SpriteMeshType.Tight
        );

        Debug.Log($"Texture size: {texture.width}x{texture.height}");

        // Check texture validity
        if (texture.width <= 2 || texture.height <= 2)
        {
            Debug.LogError("Texture failed to load properly!");
        }

        return newSprite;
    }
}

[System.Serializable]
class ShopItems
{
    public Dictionary<string, object>[] m_Items;
}

class ItemImage
{
    public ShopItem m_Item;
    public byte[] m_ImageBytes;

    public ItemImage(ShopItem item, byte[] imageBytes)
    {
        m_Item = item;
        m_ImageBytes = imageBytes;
    }
}