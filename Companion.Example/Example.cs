using RealGoodApps.Companion.Attributes;

namespace Companion.Example
{
    public class TheApi
    {
        public static readonly TheApi Instance = new TheApi();

        public void TotallyPublic()
        {
        }

        [CompanionType(typeof(Foo))]
        [CompanionType(typeof(Bar))]
        public void OnlyAvailableToFooAndBar()
        {
        }
    }

    public class Foo
    {
        public static void Test()
        {
            TheApi.Instance.TotallyPublic();
            TheApi.Instance.OnlyAvailableToFooAndBar();
        }
    }

    public class Bar
    {
        public static void Test()
        {
            TheApi.Instance.TotallyPublic();
            TheApi.Instance.OnlyAvailableToFooAndBar();
        }
    }

    public class Baz
    {
        public void TryToMakeACat()
        {
            TheApi.Instance.TotallyPublic();

            // all these are compile errors
            // TheApi.Instance.OnlyAvailableToFooAndBar();
            // Action a = TheApi.Instance.OnlyAvailableToFooAndBar;
            //
            // void Hello()
            // {
            //     TheApi.Instance.OnlyAvailableToFooAndBar();
            //     Action b = TheApi.Instance.OnlyAvailableToFooAndBar;
            // }
        }
    }
}
