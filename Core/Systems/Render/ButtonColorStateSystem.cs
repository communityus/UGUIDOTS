using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace UGUIDots.Render.Systems {

    [UpdateInGroup(typeof(MeshUpdateGroup))]
    public class ButtonColorStateSystem : SystemBase {

        private EntityCommandBufferSystem cmdBufferSystem;
        private EntityQuery buttonColorQuery;

        protected override void OnCreate() {
            buttonColorQuery = GetEntityQuery(new EntityQueryDesc {
                All = new [] { 
                    ComponentType.ReadOnly<AppliedColor>(), ComponentType.ReadOnly<ColorStates>(),
                    ComponentType.ReadOnly<ButtonVisual>()
                },
            });

            cmdBufferSystem = World.GetOrCreateSystem<BeginPresentationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate() {
            var cmdBuffer = cmdBufferSystem.CreateCommandBuffer().ToConcurrent();

            Dependency = Entities.WithStoreEntityQueryInField(ref buttonColorQuery).
                ForEach((Entity entity, in AppliedColor c0, in ColorStates c1,  in ButtonVisual c3) => {

                bool delta = true;
                Color32 color = default;

                var currentColor = c0.Value.ToNormalizedFloat4();
                
                // TODO: Redo how button clicks are registered.
                switch (c3.Value) {
                    case var _ when ButtonVisualState.Hover == c3.Value && 
                        !currentColor.Equals(c1.HighlightedColor.ToNormalizedFloat4()):
                        color = c1.HighlightedColor;
                        break;

                    case var _ when ButtonVisualState.Pressed == c3.Value &&
                        !currentColor.Equals(c1.PressedColor.ToNormalizedFloat4()):
                        color = c1.PressedColor;
                        break;

                    case var _ when ButtonVisualState.None == c3.Value &&
                        !currentColor.Equals(c1.DefaultColor.ToNormalizedFloat4()):
                        color = c1.DefaultColor;
                        break;
                    default:
                        delta = false;
                        break;
                } 

                if (delta) {
                    cmdBuffer.SetComponent(entity.Index, entity, new AppliedColor { Value = color });
                    cmdBuffer.AddComponent<UpdateVertexColorTag>(entity.Index, entity);
                }
            }).WithBurst().ScheduleParallel(Dependency);

            cmdBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
