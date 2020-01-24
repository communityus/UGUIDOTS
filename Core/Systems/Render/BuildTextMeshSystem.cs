﻿using UGUIDots.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace UGUIDots.Render.Systems {

    [UpdateInGroup(typeof(MeshBatchingGroup))]
    public class BuildTextVertexSystem : JobComponentSystem {

        [BurstCompile]
        private struct BuildGlyphMapJob : IJobForEachWithEntity<FontID> {

            [WriteOnly]
            public NativeHashMap<int, Entity>.ParallelWriter GlyphMap;

            public void Execute(Entity entity, int index, ref FontID c0) {
                GlyphMap.TryAdd(c0.Value, entity);
            }
        }

        [BurstCompile]
        private struct BuildTextMeshJob : IJobChunk {

            [ReadOnly] public NativeHashMap<int, Entity> GlyphMap;
            [ReadOnly] public BufferFromEntity<GlyphElement> GlyphData;
            [ReadOnly] public ComponentDataFromEntity<FontFaceInfo> FontFaces;

            [ReadOnly] public ArchetypeChunkEntityType EntityType;
            [ReadOnly] public ArchetypeChunkBufferType<CharElement> CharBufferType;
            [ReadOnly] public ArchetypeChunkComponentType<TextOptions> TextOptionType;
            [ReadOnly] public ArchetypeChunkComponentType<TextFontID> TxtFontIDType;
            [ReadOnly] public ArchetypeChunkComponentType<AppliedColor> ColorType;
            [ReadOnly] public ArchetypeChunkComponentType<LocalToWorld> LTWType;

            public ArchetypeChunkBufferType<MeshVertexData> MeshVertexDataType;
            public ArchetypeChunkBufferType<TriangleIndexElement> TriangleIndexType;

            public EntityCommandBuffer.Concurrent CmdBuffer;

            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex) {
                var textBufferAccessor    = chunk.GetBufferAccessor(CharBufferType);
                var vertexBufferAccessor  = chunk.GetBufferAccessor(MeshVertexDataType);
                var triangleIndexAccessor = chunk.GetBufferAccessor(TriangleIndexType);
                var textOptions           = chunk.GetNativeArray(TextOptionType);
                var txtFontIDs            = chunk.GetNativeArray(TxtFontIDType);
                var entities              = chunk.GetNativeArray(EntityType);
                var colors                = chunk.GetNativeArray(ColorType);
                var ltws                  = chunk.GetNativeArray(LTWType);

                for (int i = 0; i < chunk.Count; i++) {
                    var text       = textBufferAccessor[i].AsNativeArray();
                    var vertices   = vertexBufferAccessor[i];
                    var indices    = triangleIndexAccessor[i];
                    var fontID     = txtFontIDs[i].Value;
                    var textOption = textOptions[i];
                    var color      = colors[i].Value.ToNormalizedFloat4();

                    vertices.Clear();
                    indices.Clear();

                    var glyphTableExists  = GlyphMap.TryGetValue(fontID, out var glyphEntity);
                    var glyphBufferExists = GlyphData.Exists(glyphEntity);

                    if (glyphTableExists && glyphBufferExists) {
                        var scale = ltws[i].AverageScale();
                        var glyphData = GlyphData[glyphEntity].AsNativeArray();
                        TextMeshGenerationUtil.BuildTextMesh(ref vertices, ref indices, in text,
                            in glyphData, new float2(0, 0), scale, textOption.Style, color);
                    }

                    var textEntity = entities[i];
                    CmdBuffer.RemoveComponent<BuildTextTag>(textEntity.Index, textEntity);
                }
            }
        }

        private EntityQuery glyphQuery, textQuery;
        private EntityCommandBufferSystem cmdBufferSystem;

        protected override void OnCreate() {
            glyphQuery = GetEntityQuery(new EntityQueryDesc {
                All = new [] {
                    ComponentType.ReadOnly<GlyphElement>(),
                    ComponentType.ReadOnly<FontID>()
                }
            });

            textQuery = GetEntityQuery(new EntityQueryDesc {
                All = new [] {
                    ComponentType.ReadWrite<MeshVertexData>(),
                    ComponentType.ReadWrite<TriangleIndexElement>(),
                    ComponentType.ReadOnly<CharElement>(),
                    ComponentType.ReadOnly<TextOptions>(),
                    ComponentType.ReadOnly<BuildTextTag>(),
                }
            });

            cmdBufferSystem = World.GetOrCreateSystem<BeginPresentationEntityCommandBufferSystem>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            var glyphMap = new NativeHashMap<int, Entity>(glyphQuery.CalculateEntityCount(), Allocator.TempJob);

            var glyphMapDeps = new BuildGlyphMapJob {
                GlyphMap = glyphMap.AsParallelWriter()
            }.Schedule(glyphQuery, inputDeps);

            var textMeshDeps       = new BuildTextMeshJob {
                GlyphMap           = glyphMap,
                GlyphData          = GetBufferFromEntity<GlyphElement>(true),
                FontFaces          = GetComponentDataFromEntity<FontFaceInfo>(true),
                EntityType         = GetArchetypeChunkEntityType(),
                CharBufferType     = GetArchetypeChunkBufferType<CharElement>(true),
                TextOptionType     = GetArchetypeChunkComponentType<TextOptions>(true),
                TxtFontIDType      = GetArchetypeChunkComponentType<TextFontID>(true),
                ColorType          = GetArchetypeChunkComponentType<AppliedColor>(true),
                LTWType            = GetArchetypeChunkComponentType<LocalToWorld>(true),
                MeshVertexDataType = GetArchetypeChunkBufferType<MeshVertexData>(),
                TriangleIndexType  = GetArchetypeChunkBufferType<TriangleIndexElement>(),
                CmdBuffer          = cmdBufferSystem.CreateCommandBuffer().ToConcurrent()
            }.Schedule(textQuery, glyphMapDeps);

            var finalDeps = glyphMap.Dispose(textMeshDeps);

            cmdBufferSystem.AddJobHandleForProducer(finalDeps);

            return finalDeps;
        }
    }
}
