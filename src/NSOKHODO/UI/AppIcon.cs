using System;
using System.Drawing;
using System.Windows.Forms;

namespace NSOKHODO.UI
{
    /// <summary>
    /// Icon cua ung dung cho MOI cua so. Truoc day chi <see cref="MainForm"/> dat icon, cac hop
    /// thoai con lai deo icon mac dinh cua WinForms - nhin ra la hai ung dung khac nhau tren
    /// thanh tac vu / Alt+Tab.
    ///
    /// <para>Nap MOT LAN roi dung lai: doc resource cho moi hop thoai la phi. Moi cua so nhan mot
    /// ban <c>Clone()</c> - <see cref="Form"/> huy icon cua no khi dong, chia chung mot the hien se
    /// lam cac cua so mo sau do mat icon.</para>
    /// </summary>
    internal static class AppIcon
    {
        private static Icon _cached;
        private static bool _tried;

        /// <summary>Icon app, hoac <c>null</c> neu khong lay duoc (khong nem loi).</summary>
        public static Icon Get()
        {
            if (_tried) return _cached;
            _tried = true;
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                using (var s = asm.GetManifestResourceStream("NSOKHODO.AppIcon.ico"))
                {
                    if (s != null) { _cached = new Icon(s); return _cached; }
                }
                _cached = Icon.ExtractAssociatedIcon(asm.Location);
            }
            catch { _cached = null; }
            return _cached;
        }

        /// <summary>Gan icon app cho mot cua so. Goi trong constructor, sau khi dat Text.</summary>
        public static void Apply(Form f)
        {
            if (f == null) return;
            try
            {
                var ico = Get();
                if (ico != null) f.Icon = (Icon)ico.Clone();
            }
            catch { }
        }
    }
}
