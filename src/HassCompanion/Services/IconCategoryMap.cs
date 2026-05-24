namespace HassCompanion.Services;

/// <summary>
/// Manual categorization of ~300 relevant Material Design icons into household-friendly categories.
/// </summary>
public static class IconCategoryMap
{
    public static readonly IReadOnlyList<IconCategory> Categories =
    [
        new("Haushalt", "Home", [
            "Home", "CleaningServices", "Bed", "Bathtub", "Chair", "Weekend", "Shower",
            "SingleBed", "KingBed", "DoorFront", "DoorBack", "DoorSliding", "Window",
            "Curtains", "CurtainsClosed", "Garage", "Roofing", "Foundation",
            "Iron", "LocalLaundryService", "DryCleaningOutlet", "Checkroom",
            "Soap", "WaterDrop", "Countertops", "TableBar", "TableRestaurant",
            "Lamp", "Light", "Lightbulb", "WbIncandescent", "ElectricalServices",
            "Power", "Outlet", "Cable", "SensorDoor", "SensorWindow",
            "Stairs", "Elevator", "MeetingRoom", "LivingOutlined", "Deck",
            "Balcony", "OtherHouses", "Cottage", "Warehouse", "Storefront",
            "ReduceCapacity", "Shelves", "Inventory2"
        ]),

        new("Küche", "Kitchen", [
            "Kitchen", "Restaurant", "RestaurantMenu", "LocalDining", "DiningOutlined",
            "RiceBowl", "RamenDining", "LunchDining", "BrunchDining", "DinnerDining",
            "SetMeal", "Tapas", "Bento", "EggAlt", "Egg", "BakeryDining",
            "CakeOutlined", "Icecream", "Cookie",
            "LocalCafe", "Coffee", "CoffeeMaker", "EmojiFoodBeverage",
            "LocalBar", "Liquor", "WineBar", "SportsBar", "NightlifeOutlined",
            "WaterDamage", "LocalDrinkOutlined", "FreeBreakfast",
            "Blender", "Microwave", "Oven", "SoupKitchen",
            "ShoppingCart", "ShoppingBasket", "LocalGroceryStore", "Store"
        ]),

        new("Garten", "Yard", [
            "Yard", "Grass", "Park", "Forest", "NaturePeople", "Nature",
            "LocalFlorist", "FilterVintage", "Spa", "EmojiNature",
            "WbSunny", "WaterDrop", "Opacity", "Pool", "HotTub",
            "Fence", "DeckOutlined", "Grill", "OutdoorGrill",
            "Agriculture", "Compost", "EnergySavingsLeaf", "Eco",
            "Terrain", "Landscape"
        ]),

        new("Tiere", "Pets", [
            "Pets", "CrueltyFree", "EmojiNature",
            "SetMeal", "RiceBowl", "WaterDrop"
        ]),

        new("Müll & Recycling", "Delete", [
            "Delete", "DeleteForever", "DeleteSweep",
            "Recycling", "Compost", "LocalShipping",
            "Inventory2", "MoveToInbox", "Outbox", "Archive",
            "DeleteOutline"
        ]),

        new("Werkzeug & Reparatur", "Build", [
            "Build", "Handyman", "Construction", "Hardware", "Plumbing",
            "Carpenter", "Architecture", "FormatPaint", "ImagesearchRoller",
            "Brush", "ColorLens", "Palette",
            "Settings", "Tune", "DisplaySettings",
            "EngineeringOutlined", "PrecisionManufacturing", "SettingsInputComponent"
        ]),

        new("Kinder & Schule", "ChildCare", [
            "ChildCare", "ChildFriendly", "FamilyRestroom",
            "School", "MenuBook", "AutoStories", "LibraryBooks", "Book", "ImportContacts",
            "Draw", "Edit", "EditNote", "Create",
            "Backpack", "HistoryEdu", "Science", "Calculate", "Abc",
            "Piano", "MusicNote", "MusicVideo", "Headphones",
            "SportsEsports", "SportsScore", "EmojiEvents",
            "Toys", "SmartToy", "CatchingPokemon", "VideogameAsset"
        ]),

        new("Einkauf & Finanzen", "ShoppingCart", [
            "ShoppingCart", "ShoppingBag", "ShoppingBasket",
            "Store", "Storefront", "LocalMall", "LocalGroceryStore",
            "Receipt", "ReceiptLong", "CreditCard", "Payment", "Payments",
            "AccountBalance", "AccountBalanceWallet", "Savings",
            "MonetizationOn", "PriceCheck", "Sell", "LocalOffer", "Loyalty",
            "LocalAtm", "Euro", "CurrencyExchange"
        ]),

        new("Gesundheit & Pflege", "HealthAndSafety", [
            "HealthAndSafety", "MedicalServices", "Medication", "LocalPharmacy",
            "Vaccines", "Healing", "MonitorHeart", "Favorite", "FavoriteBorder",
            "FitnessCenter", "SportsGymnastics", "DirectionsRun", "DirectionsBike",
            "SelfImprovement", "Spa", "Face", "FaceRetouchingNatural",
            "Sanitizer", "Soap", "CleanHands", "WashOutlined",
            "Accessibility", "AccessibilityNew"
        ]),

        new("Transport & Auto", "DirectionsCar", [
            "DirectionsCar", "CarRepair", "CarCrash", "ElectricCar", "LocalCarWash",
            "LocalGasStation", "EvStation", "Garage",
            "TwoWheeler", "DirectionsBike", "PedalBike", "ElectricBike", "ElectricScooter",
            "DirectionsBus", "DirectionsTransit", "Train", "Tram", "Flight",
            "LocalParking", "NoCrash", "TireRepair"
        ]),

        new("Technik & Geräte", "Devices", [
            "Devices", "Computer", "DesktopWindows", "Laptop", "TabletMac", "PhoneAndroid",
            "Tv", "Monitor", "SmartScreen", "Cast", "Router", "Wifi", "WifiOff",
            "Bluetooth", "BluetoothConnected", "UsbOutlined", "Memory",
            "Print", "Scanner", "CameraAlt", "Videocam",
            "BatteryFull", "BatteryChargingFull", "PowerSettingsNew",
            "SmartHome", "SensorsOutlined", "Thermostat"
        ]),

        new("Zeit & Organisation", "Schedule", [
            "Schedule", "AccessTime", "Timer", "Timelapse", "HourglassEmpty",
            "CalendarToday", "CalendarMonth", "DateRange", "Event", "EventAvailable",
            "Alarm", "AlarmOn", "NotificationsActive",
            "Checklist", "ChecklistRtl", "AssignmentTurnedIn", "Assignment", "Task", "TaskAlt",
            "PlaylistAddCheck", "DoneAll", "Done", "CheckCircle",
            "Flag", "FlagCircle", "Star", "Grade", "EmojiEvents",
            "Bookmark", "BookmarkAdd", "TurnedIn"
        ])
    ];

    /// <summary>
    /// Quick lookup: icon name → category name
    /// </summary>
    private static readonly Lazy<Dictionary<string, string>> _iconToCategory = new(() =>
    {
        var dict = new Dictionary<string, string>(400);
        foreach (var cat in Categories)
        {
            foreach (var icon in cat.Icons)
            {
                dict.TryAdd(icon, cat.Name);
            }
        }
        return dict;
    });

    /// <summary>
    /// Get the category for a given icon name, or null if uncategorized.
    /// </summary>
    public static string? GetCategory(string iconName)
        => _iconToCategory.Value.TryGetValue(iconName, out var cat) ? cat : null;

    /// <summary>
    /// Get all icons for a specific category.
    /// </summary>
    public static IReadOnlyList<string> GetIcons(string categoryName)
        => Categories.FirstOrDefault(c => c.Name == categoryName)?.Icons ?? [];
}

/// <summary>
/// A named category of icons.
/// </summary>
public record IconCategory(string Name, string CategoryIcon, IReadOnlyList<string> Icons);
