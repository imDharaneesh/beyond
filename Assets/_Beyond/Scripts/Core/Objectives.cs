using System;
using UnityEngine;

namespace Beyond.Core
{
    public static class Objectives
    {
        public static string Title { get; private set; } = "";
        public static string Progress { get; private set; } = "";
        public static Vector3? Target { get; private set; }
        public static event Action Changed;

        public static void Set(string title, string progress = "", Vector3? target = null)
        {
            Title = title;
            Progress = progress;
            Target = target;
            Changed?.Invoke();
        }

        public static void SetTarget(Vector3? target)
        {
            Target = target;
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
