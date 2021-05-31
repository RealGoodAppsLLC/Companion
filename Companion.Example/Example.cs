using System;
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

        [CompanionTypeGetter(typeof(Foo))]
        [CompanionTypeSetter(typeof(Bar))]
        public int PropForFooAndBar { get; set; }

        [CompanionType("Companion.Example.Baz")]
        [CompanionType("Companion.Example.Foo.Nested")]
        [CompanionType(typeof(Bar.OtherNested))]
        public void OnlyAvailableToBazAndNested()
        {
        }
    }

    public class SomethingWithAPublicishConstructor
    {
        [CompanionType(typeof(Foo))]
        public SomethingWithAPublicishConstructor()
        {

        }
    }

    public class Foo
    {
        public static void Test()
        {
            TheApi.Instance.TotallyPublic();
            TheApi.Instance.OnlyAvailableToFooAndBar();

            var something = new SomethingWithAPublicishConstructor();
            var propValue = TheApi.Instance.PropForFooAndBar;

            // this is a compile error
            // TheApi.Instance.OnlyAvailableToBazAndNested();
        }

        public class Nested
        {
            public static void Test()
            {
                TheApi.Instance.OnlyAvailableToBazAndNested();
            }
        }
    }

    public class Bar
    {
        public static void Test()
        {
            TheApi.Instance.TotallyPublic();
            TheApi.Instance.OnlyAvailableToFooAndBar();
            TheApi.Instance.PropForFooAndBar = 5;

            // all these are compile errors
            // var something = new SomethingWithAPublicishConstructor();
            // Func<SomethingWithAPublicishConstructor> somethingFunc = () => new SomethingWithAPublicishConstructor();
            // TheApi.Instance.OnlyAvailableToBazAndNested();
        }

        public class OtherNested
        {
            public static void Test()
            {
                TheApi.Instance.OnlyAvailableToBazAndNested();
            }
        }
    }

    public class Baz
    {
        public void TryToMakeACat()
        {
            TheApi.Instance.TotallyPublic();
            TheApi.Instance.OnlyAvailableToBazAndNested();

            // all these are compile errors
            // TheApi.Instance.OnlyAvailableToFooAndBar();
            // Action a = TheApi.Instance.OnlyAvailableToFooAndBar;
            //
            // void Hello()
            // {
            //     TheApi.Instance.OnlyAvailableToFooAndBar();
            //     Action b = TheApi.Instance.OnlyAvailableToFooAndBar;
            // }
            //
            // TheApi.Instance.PropForFooAndBar = 5;
            // var prop = TheApi.Instance.PropForFooAndBar;
        }
    }
}
