using OpenCvSharp;

namespace TrustNTE.Services.Fishing
{
    internal static class FishingSceneMap
    {
        // ── 模板名称 ──
        public const string TplFishS = "Nte_Fish_S";
        public const string TplFishA = "Nte_Fish_A";
        public const string TplFishB = "Nte_Fish_B";
        public const string TplCastRod = "Nte_CastRod";
        public const string TplStartFishing = "Nte_StartFishing";
        public const string TplFishingInteract = "Nte_FishingInteract";
        public const string TplOverflow = "Nte_WarehouseOverflow";
        public const string TplBaitEmpty = "Nte_FishBaitInsufficient";
        public const string TplNoFish = "Nte_NoFishObtainedYet";
        public const string TplOneClickSell = "Nte_OneClickSellFish";
        public const string TplSellSuccess = "Nte_SellSuccess";
        public const string TplReturnFlow = "Nte_ReturnFlowFishCabin";
        public const string TplUniversalBait = "Nte_UniversalFishBait";
        public const string TplMaxBait = "Nte_MaxFishBait";
        public const string TplPurchase = "Nte_Purchase";
        public const string TplPurchasePrompt = "Nte_PurchasePrompt";
        public const string TplItemObtained = "Nte_ItemObtained";
        public const string TplTackleShop = "Nte_FishingTackleShop";
        public const string TplConfirmChange = "Nte_ConfirmChange";
        public const string TplChangeBaitBuy = "Nte_ChangeFishBaitPurchase";
        public const string TplFishCabin = "Nte_FishCabin";

        // ── ROI 区域 (1080P) ──
        public static readonly Rect CastRodRect1 = new(1741, 940, 78, 72);
        public static readonly Rect CastRodRect2 = new(1642, 932, 93, 94);
        public static readonly Rect StartRect = new(1546, 912, 138, 58);
        public static readonly Rect InteractRect = new(1133, 537, 168, 104);
        public static readonly Rect FishResultRect = new(1026, 322, 136, 115);
        public static readonly Rect BaitEmptyRect = new(909, 516, 74, 54);
        public static readonly Rect OverflowRect = new(668, 513, 235, 52);
        public static readonly Rect FishCabinRect = new(111, 383, 71, 59);
        public static readonly Rect NoFishRect = new(601, 611, 79, 45);
        public static readonly Rect OneClickSellRect = new(1014, 948, 105, 36);
        public static readonly Rect SellSuccessRect = new(657, 593, 127, 44);
        public static readonly Rect ReturnFlowRect = new(267, 250, 139, 45);
        public static readonly Rect UniversalBaitRectA = new(87, 273, 67, 35);
        public static readonly Rect UniversalBaitRectB = new(291, 277, 81, 32);
        public static readonly Rect MaxBaitRect = new(1801, 926, 55, 55);
        public static readonly Rect PurchaseRect = new(1579, 1014, 65, 32);
        public static readonly Rect PromptRect = new(1127, 687, 61, 48);
        public static readonly Rect ItemObtainedRect = new(909, 455, 101, 28);
        public static readonly Rect TackleShopRect = new(176, 34, 67, 51);
        public static readonly Rect ConfirmChangeRect = new(1139, 690, 61, 31);
        public static readonly Rect ChangeBaitBuyRect = new(1130, 684, 73, 42);

        // ── 固定点击坐标 ──
        public const int SellConfirmX = 1163, SellConfirmY = 702;
        public const int PromptConfirmX = 1150, PromptConfirmY = 708;
        public const int ChangeBaitBuyClickX = 751, ChangeBaitBuyClickY = 706;

        // ── 魔法数字收口 ──
        public const int CastCooldownMs = 1000; 
        public const int InteractCooldownMs = 400;
        public const int ControlFrameDelayMs = 30;
        public const int IdleFrameDelayMs = 50;
        public const int SellConfirmDelayMs = 1200;
    }
}