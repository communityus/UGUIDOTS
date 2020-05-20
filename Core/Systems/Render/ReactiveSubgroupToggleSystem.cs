﻿using Unity.Entities;

namespace UGUIDots.Render.Systems {

    /// <summary>
    /// Removes the DisabledTag and actually marks the subgroup as disabled.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public class ReactiveSubgroupToggleSystem : SystemBase {
        private EntityCommandBufferSystem cmdBufferSystem;

        protected override void OnCreate() {
            cmdBufferSystem = World.GetOrCreateSystem<BeginPresentationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate() {
            var cmdBuffer = cmdBufferSystem.CreateCommandBuffer().ToConcurrent();

            Dependency = Entities.WithAll<DisableRenderingTag, Disabled>().ForEach(
                (Entity entity, int entityInQueryIndex) => {
                cmdBuffer.RemoveComponent<DisableRenderingTag>(entityInQueryIndex, entity);
            }).ScheduleParallel(Dependency);

            Dependency = Entities.WithAll<EnableRenderingTag>().ForEach((Entity entity, int entityInQueryIndex) => {
                cmdBuffer.RemoveComponent<EnableRenderingTag>(entityInQueryIndex, entity);
            }).ScheduleParallel(Dependency);

            cmdBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
