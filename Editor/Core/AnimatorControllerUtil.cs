using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;

namespace BlendshapeClipFixer.Editor
{
    public static class AnimatorControllerUtil
    {
        public static HashSet<AnimationClip> CollectAnimationClips(AnimatorController controller)
        {
            var clips = new HashSet<AnimationClip>();
            if (controller == null) return clips;

            foreach (var layer in controller.layers)
            {
                if (layer?.stateMachine == null) continue;
                CollectFromStateMachine(layer.stateMachine, clips, new HashSet<BlendTree>());
            }

            return clips;
        }

        private static void CollectFromStateMachine(AnimatorStateMachine sm, HashSet<AnimationClip> clips, HashSet<BlendTree> visitedTrees)
        {
            foreach (var st in sm.states)
                CollectFromMotion(st.state.motion, clips, visitedTrees);

            foreach (var child in sm.stateMachines)
                if (child.stateMachine != null)
                    CollectFromStateMachine(child.stateMachine, clips, visitedTrees);
        }

        private static void CollectFromMotion(Motion motion, HashSet<AnimationClip> clips, HashSet<BlendTree> visitedTrees)
        {
            if (motion == null) return;

            if (motion is AnimationClip ac) { clips.Add(ac); return; }

            if (motion is BlendTree bt)
            {
                if (!visitedTrees.Add(bt)) return;
                foreach (var ch in bt.children)
                    CollectFromMotion(ch.motion, clips, visitedTrees);
            }
        }

        public static int ReplaceClipsInController(AnimatorController controller, Dictionary<AnimationClip, AnimationClip> clipMap)
        {
            if (controller == null || clipMap == null || clipMap.Count == 0) return 0;

            int rewired = 0;
            foreach (var layer in controller.layers)
            {
                if (layer?.stateMachine == null) continue;
                rewired += ReplaceInStateMachine(layer.stateMachine, clipMap, new HashSet<BlendTree>());
            }
            return rewired;
        }

        private static int ReplaceInStateMachine(AnimatorStateMachine sm, Dictionary<AnimationClip, AnimationClip> clipMap, HashSet<BlendTree> visitedTrees)
        {
            int rewired = 0;

            var states = sm.states;
            for (int i = 0; i < states.Length; i++)
            {
                var st = states[i].state;
                if (st == null) continue;

                var newMotion = ReplaceInMotion(st.motion, clipMap, visitedTrees, ref rewired);
                if (newMotion != st.motion)
                    st.motion = newMotion;
            }

            foreach (var child in sm.stateMachines)
                if (child.stateMachine != null)
                    rewired += ReplaceInStateMachine(child.stateMachine, clipMap, visitedTrees);

            return rewired;
        }

        private static Motion ReplaceInMotion(Motion motion, Dictionary<AnimationClip, AnimationClip> clipMap, HashSet<BlendTree> visitedTrees, ref int rewired)
        {
            if (motion == null) return null;

            if (motion is AnimationClip ac)
            {
                if (clipMap.TryGetValue(ac, out var repl) && repl != null)
                {
                    rewired++;
                    return repl;
                }
                return motion;
            }

            if (motion is BlendTree bt)
            {
                if (!visitedTrees.Add(bt)) return motion;

                var children = bt.children;
                for (int i = 0; i < children.Length; i++)
                {
                    var ch = children[i];
                    var replacedChildMotion = ReplaceInMotion(ch.motion, clipMap, visitedTrees, ref rewired);
                    if (replacedChildMotion != ch.motion)
                    {
                        ch.motion = replacedChildMotion;
                        children[i] = ch;
                    }
                }
                bt.children = children;
                return motion;
            }

            return motion;
        }
    }
}