namespace Gazon.Core
{
    /// <summary>
    /// Флаги "открыта модальная панель" — движение/осмотр/E блокируются, пока хоть один true.
    /// Аналог anyPanelOpen() в MVP. Каждый контроллер панели сам выставляет свой флаг в true/false
    /// при открытии/закрытии — здесь только агрегатор.
    /// </summary>
    public static class InputLock
    {
        public static bool PhoneOpen;
        public static bool BabkaOpen;
        public static bool EyeOpen;
        public static bool QteOpen;
        public static bool SmokeActive;

        public static bool AnyOpen => PhoneOpen || BabkaOpen || EyeOpen || QteOpen || SmokeActive;

        public static void ResetAll()
        {
            PhoneOpen = BabkaOpen = EyeOpen = QteOpen = SmokeActive = false;
        }
    }
}
