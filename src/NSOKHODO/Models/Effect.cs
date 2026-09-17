using System;

namespace NSOKHODO.Models
{
    /// <summary>
    /// Hieu ung dang hoat dong tren nhan vat (clone MODGAME Char.vEff / class Effect).
    /// EffectId = template id (MODGAME effTemplates[id]); phan loai qua template.type
    /// (thuc an = type 0, hoi HP = id 21...). NSOKHODO DA parse bang effTemplate tu duoi block
    /// DataSync "data" (NotMapHandler.HandleUpdateData) -> tra type qua GameState.GetEffectType.
    /// (Co che "tu HOC id thuc an"/FoodEffectId cua ban 2026-07-07 da BI BO - dung type 0 chuan hon.)
    /// ExpiresAt = het han CUC BO (UtcNow + thoi gian con lai) - luoi an toan de tu go effect
    /// het han neu server khong gui goi remove (-99).
    /// </summary>
    public class Effect
    {
        public byte EffectId { get; set; }      // = template id
        public int DurationMs { get; set; }     // thoi luong goc (ms) tu server
        public short Param { get; set; }
        public DateTime ExpiresAt { get; set; } // UTC
    }
}
