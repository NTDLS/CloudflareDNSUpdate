namespace CloudflareDNSUpdate
{
    internal class Singletons
    {
        public static long ReentrantLockValue = 0;
        public static Lock ReentrantLock = new();
    }
}
