using System;

namespace Beyond.Core
{
    public static class Messages
    {
        public static event Action<string> Posted;

        public static void Post(string text) => Posted?.Invoke(text);
    }
}
