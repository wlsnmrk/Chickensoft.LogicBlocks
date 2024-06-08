namespace Chickensoft.LogicBlocks;

using System;
using System.Collections.Generic;
using Chickensoft.Introspection;

using static Introspection.Types;

public abstract partial class LogicBlock<TState> {
  /// <summary>
  /// Adds an instance of every concrete state type in the state hierarchy to
  /// the blackboard. For this to work, the logic block and its states must be
  /// introspective types whose introspection metadata is generated by the
  /// Chickensoft.Introspection generator.
  /// </summary>
  /// <param name="logic">Logic block whose states should be preallocated.
  /// </param>
  /// <exception cref="LogicBlockException" />
  internal static void PreallocateStates(ILogicBlock<TState> logic) {
    var type = logic.GetType();
    var metadata = Graph.GetMetadata(type);

    if (metadata is not IIntrospectiveTypeMetadata introspectiveMetadata) {
      // No preallocation for non-introspective types.
      return;
    }

    if (
      Graph.GetAttribute<LogicBlockAttribute>(type) is not { } logicAttribute
    ) {
      // We're missing the logic block attribute. Introspective types must
      // have the [LogicBlock] attribute.
      throw new LogicBlockException(
        $"Logic block `{type}` is missing the " +
        $"[{nameof(LogicBlockAttribute)}] attribute."
      );
    }

    // See if logic block is an identifiable, introspective type (serializable).
    // If it is, we will throw if any of its states are not also identifiable,
    // introspective types. If we're not an identifiable, introspective type,
    // we don't need to perform additional validation for serialization —
    // just do enough to preallocate states and be done.
    var isIdentifiable = metadata is IIdentifiableTypeMetadata;

    var baseStateType = logicAttribute.StateType;
    var descendantStateTypes = Graph.GetDescendantSubtypes(baseStateType);

    // Only allocate list if we need to validate state types for serialization.
    var stateTypesNeedingAttention = isIdentifiable ?
      new HashSet<Type>(descendantStateTypes.Count + 1)
      : null;

    void cacheStateIfNeeded(Type type, IConcreteTypeMetadata metadata) {
      // Cache a pristine version of the state. Only done once per logic block
      // type (not instance). Used by the serialization system to determine
      // if it really needs to save a state.
      if (!ReferenceStates.ContainsKey(type)) {
        ReferenceStates.TryAdd(type, metadata.Factory());
      }
    }

    void discoverState(Type type) {
      if (isIdentifiable) {
        // Serializable logic block.
        var stateMetadata = Graph.GetMetadata(type);

        if (
          stateMetadata is IIntrospectiveTypeMetadata iMetadata &&
          iMetadata.Metatype.Attributes.ContainsKey(typeof(TestStateAttribute))
        ) {
          // Skip test states.
          return;
        }

        if (stateMetadata is IIdentifiableTypeMetadata idMetadata) {
          if (idMetadata is IConcreteTypeMetadata concreteMetadata) {
            cacheStateIfNeeded(type, concreteMetadata);

            // We're a serializable logic block. States should only be saved if
            // they have diverged from the reference state.
            logic.SaveObject(
              type: type,
              factory: concreteMetadata.Factory,
              referenceValue: ReferenceStates[type]
            );

            // Force persisted state to be created and added to the blackboard.
            // Reasoning: do as much heap allocation as possible during setup
            // instead of during execution.
            logic.OverwriteObject(type, concreteMetadata.Factory());
          }
        }
        else if (stateMetadata is not IIntrospectiveTypeMetadata) {
          // Logic block is serializable, but the state is not even an
          // introspective type. Add state to the list of states to mention
          // when we throw an error later.
          stateTypesNeedingAttention!.Add(type);
        }
        else if (stateMetadata is IConcreteTypeMetadata) {
          // Concrete introspective types on serializable logic blocks MUST
          // be identifiable types.
          stateTypesNeedingAttention!.Add(type);
        }

        return;
      }

      // Non-serializable logic block.

      if (Graph.GetMetadata(type) is IConcreteTypeMetadata metadata) {
        cacheStateIfNeeded(type, metadata);
        // We're not a serializable logic block. Just add the state to the
        // blackboard the normal way.
        logic.OverwriteObject(type, metadata.Factory());
      }
    }

    discoverState(baseStateType);

    foreach (var stateType in descendantStateTypes) {
      discoverState(stateType);
    }

    if (!isIdentifiable) { return; }

    if (stateTypesNeedingAttention!.Count == 0) { return; }

    var statesNeedingAttention = string.Join(", ", stateTypesNeedingAttention);

    throw new LogicBlockException(
      $"Serializable LogicBlock `{type}` has states that are not " +
      $"serializable. Please ensure the following types have the " +
      $"[{nameof(MetaAttribute)}] and [{nameof(IdAttribute)}] attributes: " +
      $"{statesNeedingAttention}."
    );
  }
}
