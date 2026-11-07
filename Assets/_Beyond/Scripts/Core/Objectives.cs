using System;

namespace Beyond.Core
{
    public static class Objectives
    {
        public static string Title { get; private set; } = "";
        public static string Progress { get; private set; } = "";
        public static event Action Changed;

        public static void Set(string title, string progress = "")
        {
            Title = title;
            Progress = progress;
            Changed?.Invoke();
        }

        public static void SetProgress(string progress)
        {
            if (progress == Progress) return;
            Progress = progress;
            Changed?.Invoke();
        }
    }
}
