﻿using UnityEngine;
using System.Collections.Generic;

namespace MoveIt
{
    public class TransformAction : BaseTransformAction
    {
    }

    public class MoveToAction : BaseTransformAction
    {
        internal Vector3 Original, Position;
        internal float AngleOriginal, Angle;
        internal bool AngleActive, HeightActive;
    }

    public abstract class BaseTransformAction : Action
    {
        public Vector3 moveDelta;
        public Vector3 center;
        public float angleDelta;
        public float snapAngle;
        public bool followTerrain;

        public bool autoCurve;
        public NetSegment segmentCurve;

        protected readonly bool containsNetwork = false;

        public HashSet<InstanceState> m_states = new HashSet<InstanceState>();

        internal bool _virtual = false;
        public bool Virtual
        {
            get => _virtual;
            set
            {
                if (value == true)
                {
                    if (_virtual == false && selection.Count < MoveItTool.Fastmove_Max)
                    {
                        _virtual = true;
                        foreach (Instance i in selection)
                        {
                            i.Virtual = true;
                        }
                    }
                }
                else
                {
                    if (_virtual == true)
                    {
                        _virtual = false;
                        foreach (Instance i in selection)
                        {
                            i.Virtual = false;
                        }
                        Do();
                        UpdateArea(GetTotalBounds(), true);
                    }
                }
            }
        }

        public BaseTransformAction()
        {
            foreach (Instance instance in selection)
            {
                if (instance.isValid)
                {
                    m_states.Add(instance.SaveToState());

                    if (instance is MoveableNode || instance is MoveableSegment)
                    {
                        containsNetwork = true;
                    }
                }
            }

            center = GetCenter();
        }

        public override void Do()
        {
            Bounds originalBounds = GetTotalBounds(false);

            Matrix4x4 matrix4x = default;
            matrix4x.SetTRS(center + moveDelta, Quaternion.AngleAxis((angleDelta + snapAngle) * Mathf.Rad2Deg, Vector3.down), Vector3.one);

            foreach (InstanceState state in m_states)
            {
                if (state.instance.isValid && !(state is SegmentState))
                {
                    state.instance.Transform(state, ref matrix4x, moveDelta.y, angleDelta + snapAngle, center, followTerrain);

                    if (autoCurve && state.instance is MoveableNode node)
                    {
                        node.AutoCurve(segmentCurve);
                    }
                }
            }

            // Move segments after the nodes have moved
            foreach (InstanceState state in m_states)
            {
                if (state.instance.isValid && state is SegmentState)
                {
                    state.instance.Transform(state, ref matrix4x, moveDelta.y, angleDelta + snapAngle, center, followTerrain);
                }
            }

            bool full = !(MoveItTool.fastMove != Event.current.shift);
            if (!full)
            {
                full = selection.Count > MoveItTool.Fastmove_Max ? true : false;
            }
            UpdateArea(originalBounds, full);
            Bounds fullbounds = GetTotalBounds(false);
            UpdateArea(fullbounds, full);
        }

        public override void Undo()
        {
            Bounds bounds = GetTotalBounds(false);

            foreach (InstanceState state in m_states)
            {
                if (!(state is SegmentState))
                {
                    state.instance.LoadFromState(state);
                }
            }

            foreach (InstanceState state in m_states)
            {
                if (state is SegmentState)
                {
                    state.instance.LoadFromState(state);
                }
            }

            UpdateArea(bounds, true);
            UpdateArea(GetTotalBounds(false), true);
        }

        public void InitialiseDrag()
        {
            MoveItTool.dragging = true;
            Virtual = false;

            foreach (InstanceState instanceState in m_states)
            {
                if (instanceState.instance is MoveableBuilding mb)
                {
                    mb.InitialiseDrag();
                }
            }
        }

        public void FinaliseDrag()
        {
            MoveItTool.dragging = false;
            Virtual = false;

            foreach (InstanceState instanceState in m_states)
            {
                if (instanceState.instance is MoveableBuilding mb)
                {
                    mb.FinaliseDrag();
                }
            }
        }

        public override void ReplaceInstances(Dictionary<Instance, Instance> toReplace)
        {
            foreach (InstanceState state in m_states)
            {
                if (toReplace.ContainsKey(state.instance))
                {
                    DebugUtils.Log("TransformAction Replacing: " + state.instance.id.RawData + " -> " + toReplace[state.instance].id.RawData);
                    state.ReplaceInstance(toReplace[state.instance]);
                }
            }
        }

        public HashSet<InstanceState> CalculateStates(Vector3 deltaPosition, float deltaAngle, Vector3 center, bool followTerrain)
        {
            Matrix4x4 matrix4x = default;
            matrix4x.SetTRS(center + deltaPosition, Quaternion.AngleAxis(deltaAngle * Mathf.Rad2Deg, Vector3.down), Vector3.one);

            HashSet<InstanceState> newStates = new HashSet<InstanceState>();

            foreach (InstanceState state in m_states)
            {
                if (state.instance.isValid)
                {
                    InstanceState newState = new InstanceState();
                    newState.instance = state.instance;
                    newState.Info = state.Info;

                    newState.position = matrix4x.MultiplyPoint(state.position - center);
                    newState.position.y = state.position.y + deltaPosition.y;

                    if (followTerrain)
                    {
                        newState.terrainHeight = TerrainManager.instance.SampleOriginalRawHeightSmooth(newState.position);
                        newState.position.y = newState.position.y + newState.terrainHeight - state.terrainHeight;
                    }

                    newState.angle = state.angle + deltaAngle;

                    newStates.Add(newState);
                }
            }
            return newStates;
        }
    }
}
