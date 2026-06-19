# Lilja.MasterData Specification

Status: Draft v1

Last updated: 2026-06-19

## 1. Purpose

Lilja.MasterData is a master-data toolchain for Unity games.

It is not a Unity runtime framework and does not require a UPM runtime package in v1. Its primary job is to convert text-based YAML master-data definitions into:

- C# source files for MasterMemory table/types
- A MasterMemory binary database
- Optional synchronized copies of those generated artifacts into a Unity client project

The toolchain is designed for a monorepo where the Unity client project and master-data project live side by side.

```text
repo/
  client/
    Assets/
    Packages/
    ProjectSettings/

  master-data/
    master/
    templates/
    dist/
    .lilja/
    lilja-master-data.yaml
```

## 2. Non-Goals

v1 must not implement the following:

- Unity runtime loader
- Addressables, Resources, StreamingAssets, AssetBundle, remote delivery, or patch-system integration
- UPM runtime package requirement
- YAML visual editor
- Table-specific converter C# generation
- Multiple definitions in one YAML file
- Directory-path-derived namespace, table name, or type name
- Struct or list fields as MasterMemory keys

Project-specific loading remains the responsibility of the Unity client project.

## 3. Architecture

### 3.1 Components

The implementation has two tool layers.

```text
Rust orchestrator CLI
  - Distributed to users as lilja-masterdata
  - Parses YAML
  - Validates schema/data
  - Generates C#
  - Expands a temporary C# builder project
  - Calls dotnet build/run
  - Performs sync/clean

C# temporary builder
  - Generated/extracted into .lilja/temp during build
  - Compiled every build or when inputs change
  - Contains generated Master types
  - Uses MasterMemory AppendDynamic to build the binary
```

The orchestrator should be written in Rust because a future Tauri editor will use Rust as its backend. Core logic should be structured so the CLI and Tauri backend can share it.

Suggested Rust crate split:

```text
crates/
  lilja_master_data_core/      # config, scan, parse, validate, codegen, sync model
  lilja_master_data_cli/       # CLI binary
  lilja_master_data_tauri/     # future app backend adapter
```

### 3.1.1 Package Repository Layout

The Lilja.MasterData package repository should use this layout:

```text
lilja.master-data/
  README.md
  docs/
    Lilja.MasterData.spec.md
  crates/
    lilja_master_data_core/
      Cargo.toml
      src/
    lilja_master_data_cli/
      Cargo.toml
      src/
    lilja_master_data_tauri/
      Cargo.toml
      src/
  embedded/
    builder/
      Lilja.MasterData.GeneratedBuilder.csproj
      Program.cs
      Builder/
        MasterMemoryBinaryBuilder.cs
        BuildInput.cs
    templates/
      csharp/
        table.cs.tpl
        struct.cs.tpl
        enum.cs.tpl
  testdata/
    valid/
    invalid/
  tests/
    cli/
    codegen/
    validation/
```

`crates/lilja_master_data_core` must not depend on CLI argument parsing or Tauri APIs.

`crates/lilja_master_data_cli` owns command-line parsing, process exit codes, and console formatting.

`crates/lilja_master_data_tauri` is a future adapter that exposes core operations through Tauri commands. It should not fork validation or code generation behavior.

`embedded/builder` contains normal C# files in source control. The Rust binary embeds these files at compile time and writes them to `.lilja/temp/builder` at runtime.

`embedded/templates/csharp` contains the built-in C# outer templates for table, struct, and enum definitions.

### 3.2 Distribution

The CLI is distributed as a single executable per OS/architecture.

The executable embeds:

- Built-in C# output templates
- C# temporary builder project files
- Built-in diagnostic/help text

At runtime, embedded C# builder files are written to `.lilja/temp/builder`.

The CLI still requires .NET SDK because the generated C# master types must be compiled to build a MasterMemory binary.

Startup must verify:

- `dotnet` is on `PATH`
- `dotnet --version` succeeds
- SDK version satisfies the configured minimum, default .NET SDK 8.0+

If the SDK is missing or too old, the CLI must fail before generation with an actionable error.

### 3.3 Build Pipeline

`lilja-masterdata build <project-dir>` must perform:

1. Load `lilja-master-data.yaml`.
2. Recursively scan `<master.input>/**/*.yaml`.
3. Parse each YAML file into exactly one definition.
4. Validate global schema and all rows.
5. Generate C# source files into `csharp.output`.
6. Generate normalized build input JSON under `.lilja/temp`.
7. Expand embedded C# builder project into `.lilja/temp/builder`.
8. Copy or link generated C# sources into the builder project.
9. Run `dotnet build` for the builder project.
10. Run the builder executable with the normalized build input.
11. Verify the configured MasterMemory binary output exists.

The C# temporary builder must:

1. Load normalized build input JSON.
2. Load generated table/type metadata.
3. Resolve generated types by full name.
4. Create table row instances using uninitialized-object creation.
5. Set properties by reflection.
6. Convert sequences to `ImmutableArray<T>`.
7. Append table data via MasterMemory `AppendDynamic`.
8. Write the configured binary file.

The builder must not parse source YAML. YAML interpretation belongs to the Rust core.

## 4. Project Layout

### 4.1 Required Layout

```text
master-data/
  master/
  dist/
    cs/
    master-memory/
  .lilja/
    temp/
  lilja-master-data.yaml
```

`templates/` is optional. If no template is configured, built-in templates are used.

### 4.2 `master/` Directory Rules

`master/` is user-controlled.

Allowed:

- Arbitrary subdirectories
- Arbitrary YAML filenames
- Moving YAML files without changing generated type/table names

Not allowed:

- More than one definition per YAML file
- Deriving namespace/type/table names from the file path

The tool must determine definition identity only from YAML content.

The relative path under `master/` may be preserved only when syncing source YAML for editor/debug workflows. It must not affect generated C# or binary layout.

## 5. Configuration

Configuration file path:

```text
master-data/lilja-master-data.yaml
```

Minimal config:

```yaml
csharp:
  namespace: Game.MasterData
  output: dist/cs
  templates:
    table: templates/csharp/table.cs.tpl
    struct: templates/csharp/struct.cs.tpl
    enum: templates/csharp/enum.cs.tpl
  staticDatabaseAccessor:
    enabled: false
    expression: Game.MasterData.MasterDatabaseProvider.Current
    tableProperties:
      ItemMaster: ItemTable
      RewardMaster: RewardTable

master:
  input: master

memory:
  output: dist/master-memory
  fileName: master-data.bytes

sync:
  cs: ../client/Assets/Generated/MasterData
  memory: ../client/Assets/StreamingAssets/MasterData
```

### 5.1 `csharp.namespace`

Required.

This is not just formatting preference. The tool uses it to compute full type names:

```text
fullTypeName = csharp.namespace + "." + definitionName
```

The C# temporary builder resolves types using the full name.

Custom templates must place generated types in this namespace. If a template uses a different namespace, build must fail with a type-resolution error.

### 5.2 `csharp.output`

Required.

Relative path from the master-data project root. Generated C# files are written here.

Default if omitted may be `dist/cs`, but generated projects should write it explicitly.

### 5.3 `csharp.templates`

Optional.

If set, these files are used for generated C# files. If a template is omitted, the built-in template for that definition kind is used.

Supported keys:

```yaml
csharp:
  templates:
    table: templates/csharp/table.cs.tpl
    struct: templates/csharp/struct.cs.tpl
    enum: templates/csharp/enum.cs.tpl
```

Every template is an outer wrapper only. It must contain `{body}` exactly once.

`table`, `struct`, and `enum` use separate templates because projects may want different attributes, pragma directives, or usings for each kind.

The legacy singular key `csharp.template` is not part of v1.

### 5.4 `csharp.staticDatabaseAccessor`

Optional and disabled by default.

This setting enables project-level opt-in generation of convenience properties for MasterRef.

It is intended for projects that expose the loaded MasterMemory database through a static accessor.

Example:

```yaml
csharp:
  staticDatabaseAccessor:
    enabled: true
    expression: Game.MasterData.MasterDatabaseProvider.Current
    tableProperties:
      ItemMaster: ItemTable
      RewardMaster: RewardTable
```

`expression` is emitted as C# source code and must evaluate to the current memory database instance.

`tableProperties` maps target master record type names to table property names on the database expression.

When enabled, generated table records may include cached MasterRef convenience properties in addition to core `GetXxx(<TargetTable> table)` methods.

This feature must be completely opt-in because cache lifetime depends on project-specific master database reload behavior.

### 5.5 `master.input`

Required.

Relative path from the master-data project root. The tool scans `**/*.yaml` and `**/*.yml` beneath this directory.

### 5.6 `memory.output` and `memory.fileName`

Required.

The binary output path is:

```text
<memory.output>/<memory.fileName>
```

### 5.7 `sync`

Optional.

If present, `sync` copies generated artifacts into the Unity client project.

`build` must not copy into the Unity client project. Copying is only done by `sync`.

## 6. YAML Definition Model

Every YAML file under `master.input` must contain one definition with a `kind`.

Valid kinds:

- `enum`
- `struct`
- `table`

Unknown kinds are errors.

Each definition gets a source identity:

```text
source path relative to master.input
```

This identity is used only for diagnostics and optional sync. It is not used for generated naming.

## 7. Names and Identifiers

### 7.1 C# Identifier Rules

Names that become C# identifiers must match:

```text
^[A-Z_][A-Za-z0-9_]*$ for type names and enum members
^[A-Z_][A-Za-z0-9_]*$ for generated property names
```

v1 uses PascalCase field names directly as C# property names.

Field names must be unique within a table or struct.

Definition names must be unique across all enum/struct/table type names in the project.

### 7.2 Table Name Rules

`table` is the MasterMemory table name saved in the database binary.

It must be unique across all table definitions.

Recommended format:

```text
^[a-z][a-z0-9_]*$
```

The tool should warn, not fail, if the table name is valid YAML string but does not match the recommended format.

## 8. Enum Definitions

### 8.1 Basic Enum

```yaml
kind: enum
name: ItemType
members:
  - Consumable
  - Weapon
  - Armor
```

Generated C#:

```csharp
public enum ItemType
{
    Consumable,
    Weapon,
    Armor,
}
```

### 8.2 Enum With Explicit Values

```yaml
kind: enum
name: Rarity
underlyingType: int
members:
  - name: Normal
    value: 0
  - name: Rare
    value: 10
  - name: Epic
    value: 20
```

Generated C#:

```csharp
public enum Rarity : int
{
    Normal = 0,
    Rare = 10,
    Epic = 20,
}
```

### 8.3 Enum Validation

Errors:

- Missing `name`
- Missing or empty `members`
- Duplicate member names
- Duplicate explicit values within the same enum
- Mixed scalar members and object members
- Unsupported `underlyingType`

Supported `underlyingType` values:

- `byte`
- `short`
- `int`
- `long`

Default `underlyingType` is `int`.

YAML row values for enum fields must use member names, not numeric values.

## 9. Struct Definitions

Struct definitions describe reusable immutable value types.

```yaml
kind: struct
name: Price
fields:
  - name: Amount
    type: int
  - name: Currency
    type: string
```

Generated C#:

```csharp
[MessagePackObject]
public readonly partial record struct Price
{
    [Key(0)]
    public int Amount { get; init; }

    [Key(1)]
    public string Currency { get; init; }
}
```

### 9.1 Struct Field Rules

Struct fields may use:

- Scalar types
- Generated enum types
- Generated struct types
- `list<T>` where `T` is scalar, enum, or struct

Struct fields must not use table types.

Recursive struct definitions are errors. This includes direct and indirect cycles.

### 9.2 Struct Defaults

Generated string properties should default to `""`.

Generated `ImmutableArray<T>` properties should default to `ImmutableArray<T>.Empty`.

Other value types use their normal default.

## 10. Table Definitions

Table definitions describe MasterMemory table record types and row data.

```yaml
kind: table
table: items
typeName: ItemMaster

keys:
  primary:
    fields: [Id]
  secondary:
    - fields: [Type, Rarity]
      unique: false
    - fields: [Slug]
      unique: true

fields:
  - name: Id
    type: int
  - name: Slug
    type: string
  - name: Type
    type: ItemType
  - name: Rarity
    type: Rarity
  - name: Price
    type: Price
  - name: RewardIds
    type: list<int>

rows:
  - Id: 1
    Slug: potion
    Type: Consumable
    Rarity: Normal
    Price:
      Amount: 100
      Currency: gold
    RewardIds: [10, 11]
```

Generated C#:

```csharp
[MemoryTable("items")]
[MessagePackObject]
public sealed partial record ItemMaster
{
    [PrimaryKey]
    [Key(0)]
    public int Id { get; init; }

    [SecondaryKey(1, keyOrder: 0)]
    [Key(1)]
    public string Slug { get; init; } = "";

    [SecondaryKey(0, keyOrder: 0), NonUnique]
    [Key(2)]
    public ItemType Type { get; init; }

    [SecondaryKey(0, keyOrder: 1), NonUnique]
    [Key(3)]
    public Rarity Rarity { get; init; }

    [Key(4)]
    public Price Price { get; init; }

    [Key(5)]
    public ImmutableArray<int> RewardIds { get; init; } = ImmutableArray<int>.Empty;
}
```

### 10.1 Master Type Generation

Table records must be generated as immutable records:

```csharp
public sealed partial record <TypeName>
```

Properties must use `init`.

Do not generate `required` in v1 because Unity compatibility varies.

### 10.2 Table Field Rules

Fields may use:

- Scalar types
- Generated enum types
- Generated struct types
- `list<T>` where `T` is scalar, enum, or struct

Fields must not use table record types.

Field order in YAML is serialization order. The first field gets `[Key(0)]`, second `[Key(1)]`, and so on.

Changing field order is a binary schema change.

### 10.3 MasterRef

MasterRef declares that one or more local fields reference another table by one of its MasterMemory keys.

It generates helper methods on the source table record type. It also validates that referenced rows exist.

MasterRef does not change serialized fields or the MasterMemory binary schema.

Core MasterRef methods are always generated as methods that take the target table as an argument.

If `csharp.staticDatabaseAccessor.enabled` is true and a target table mapping exists, additional cached convenience properties are generated.

Example with a single primary key:

```yaml
kind: table
table: item_drops
typeName: ItemDropMaster

keys:
  primary:
    fields: [Id]

fields:
  - name: Id
    type: int
  - name: ItemId
    type: int

refs:
  - name: Item
    target: ItemMaster
    targetKey:
      primary: true
    fields:
      - local: ItemId
        target: Id
```

Generated helper:

```csharp
#if !LILJA_MASTERDATA_BUILD
public global::Game.MasterData.ItemMaster GetItem(global::Game.MasterData.ItemTable table)
{
    return table.FindById(ItemId);
}
#endif
```

Generated static-accessor convenience property when enabled:

```csharp
#if !LILJA_MASTERDATA_BUILD
private global::Game.MasterData.ItemMaster? _itemCache;

public global::Game.MasterData.ItemMaster Item =>
    _itemCache ??= GetItem(global::Game.MasterData.MasterDatabaseProvider.Current.ItemTable);
#endif
```

Example with a composite secondary key:

```yaml
refs:
  - name: ItemByTypeAndRarity
    target: ItemMaster
    targetKey:
      fields: [Type, Rarity]
    fields:
      - local: ItemType
        target: Type
      - local: ItemRarity
        target: Rarity
```

Example with a list-valued local field:

```yaml
fields:
  - name: RewardTagIds
    type: list<int>

refs:
  - name: RewardItems
    target: ItemMaster
    targetKey:
      primary: true
    fields:
      - local: RewardTagIds
        target: Id
```

This generates a helper that uses the record's own list property:

```csharp
#if !LILJA_MASTERDATA_BUILD
public global::System.Collections.Immutable.ImmutableArray<global::Game.MasterData.ItemMaster> GetRewardItems(
    global::Game.MasterData.ItemTable table)
{
    var builder = global::System.Collections.Immutable.ImmutableArray.CreateBuilder<global::Game.MasterData.ItemMaster>();
    foreach (var rewardTagId in RewardTagIds)
    {
        builder.Add(table.FindById(rewardTagId));
    }

    return builder.ToImmutable();
}
#endif
```

Generated static-accessor convenience property when enabled:

```csharp
#if !LILJA_MASTERDATA_BUILD
private global::System.Collections.Immutable.ImmutableArray<global::Game.MasterData.ItemMaster>? _rewardItemsCache;

public global::System.Collections.Immutable.ImmutableArray<global::Game.MasterData.ItemMaster> RewardItems =>
    _rewardItemsCache ??= GetRewardItems(global::Game.MasterData.MasterDatabaseProvider.Current.ItemTable);
#endif
```

List-valued MasterRef helpers iterate the local list property and return an immutable result. They preserve local list order. They do not deduplicate results in v1.

The target key must match either:

- The target table primary key
- One target table secondary key

Matching is by target field list in declared order.

`targetKey.primary: true` is a shortcut for the target table primary key.

`targetKey.fields` identifies a primary or secondary key by field names.

`fields` maps local fields to target key fields. The mapping length must equal the target key field count.

For each mapping entry:

- `local` must be a field on the current table
- `target` must be a field in the selected target key
- Local and target field types must be identical after type resolution
- Mapping order must match the target key order

For list-valued local fields:

- The local field type must be `list<T>`.
- The target field type must be `T`.
- A MasterRef may contain list-valued local fields only when the selected target key is unique.
- The selected target key may be the primary key.
- The selected target key may be a unique secondary key.
- The selected target key must not be a non-unique secondary key in v1.
- If the target key is composite, every mapped local field must either be scalar or `list<T>`, but v1 permits at most one list-valued local field per MasterRef.
- The generated method still takes only the target table parameter.
- The generated method uses scalar local properties directly and iterates the single list-valued local property.

The generated method name is `Get` + `refs[].name`.

`refs[].name` must be unique within the source table and must be a valid C# identifier suffix.

If static-accessor convenience properties are enabled, the generated property name is exactly `refs[].name`.

The generated cache field name is `_` + camelCase(`refs[].name`) + `Cache`.

The ref name must not collide with any table field property name or another generated member name.

If the target key is unique, the generated method returns the target record type.

If the target key is non-unique, the generated method returns:

```csharp
global::MasterMemory.RangeView<global::<namespace>.<TargetTypeName>>
```

If any local field is list-valued, the selected target key must be unique and the generated method returns:

```csharp
global::System.Collections.Immutable.ImmutableArray<global::<namespace>.<TargetTypeName>>
```

Rationale: `RangeView<T>` represents one lookup against a non-unique key. A list-valued local field such as `RewardIds` represents multiple exact key lookups. v1 models that as an immutable array of target records, not a `RangeView<T>`.

Generated MasterRef methods must be wrapped in:

```csharp
#if !LILJA_MASTERDATA_BUILD
...
#endif
```

The temporary C# builder project must define `LILJA_MASTERDATA_BUILD`.

Rationale: the temporary builder needs generated record types for binary construction, but it does not need Unity-side navigation helpers. Excluding MasterRef helpers from the temporary builder avoids unnecessary dependency on generated MasterMemory table API shape during binary build.

Static-accessor convenience properties must also be wrapped in `#if !LILJA_MASTERDATA_BUILD`.

The cache is per record instance. The generated code must not attempt to invalidate cache values when the project reloads its memory database. Projects that reload master data at runtime must either avoid this feature or clear/recreate record instances with the database.

MasterRef validation errors:

- Unknown target table type
- Unknown local field
- Unknown target key field
- Target key does not match any primary or secondary key
- Local/target key field count mismatch
- Local/target field type mismatch
- Duplicate generated method name
- Referenced row does not exist
- List-valued local ref maps to a non-unique secondary key
- List-valued local ref maps to a target field whose type is not the local element type
- More than one list-valued local field in one MasterRef
- Static database accessor enabled but target table mapping is missing
- Generated convenience property name collides with an existing field or generated member

For unique target keys, row validation requires exactly one target row.

For non-unique target keys, row validation requires at least one target row.

For list-valued local refs, row validation checks each element in the local list independently. Every element must match exactly one target row by the selected unique key.

## 11. Type System

### 11.1 Scalar Types

Supported scalar types:

```text
bool
int
long
float
double
string
```

These map to C#:

```text
bool   -> bool
int    -> int
long   -> long
float  -> float
double -> double
string -> string
```

### 11.2 Generated Types

Generated enum and struct types are referenced by their `name`.

```yaml
type: ItemType
type: Price
```

Name resolution is project-wide, not directory-local.

### 11.3 List Types

YAML type syntax:

```text
list<T>
```

C# type:

```text
System.Collections.Immutable.ImmutableArray<T>
```

Examples:

```yaml
- name: RewardIds
  type: list<int>

- name: Prices
  type: list<Price>
```

Generated:

```csharp
public ImmutableArray<int> RewardIds { get; init; } = ImmutableArray<int>.Empty;
public ImmutableArray<Price> Prices { get; init; } = ImmutableArray<Price>.Empty;
```

Nested lists are not supported in v1.

```text
list<list<int>> is an error
```

## 12. Key Specification

### 12.1 Primary Key

Every table must define a primary key.

Single primary key:

```yaml
keys:
  primary:
    fields: [Id]
```

Composite primary key:

```yaml
keys:
  primary:
    fields: [ItemId, Level]
```

Generated attributes:

```csharp
[PrimaryKey(keyOrder: 0)]
public int ItemId { get; init; }

[PrimaryKey(keyOrder: 1)]
public int Level { get; init; }
```

If a primary key has only one field, `keyOrder` may be omitted in generated code, but generating it consistently is also allowed.

Primary keys are unique by default.

### 12.2 Non-Unique Primary Key

v1 does not support non-unique primary keys.

If needed later, this may map to `[PrimaryKey, NonUnique]`, but v1 must reject:

```yaml
keys:
  primary:
    fields: [Id]
    unique: false
```

### 12.3 Secondary Key

Secondary keys are optional.

```yaml
keys:
  secondary:
    - fields: [Type]
      unique: false
    - fields: [Slug]
      unique: true
```

The list order determines MasterMemory secondary key index number.

The first secondary key is index `0`, the second is index `1`, and so on.

`unique: false` adds `[NonUnique]`.

`unique: true` does not add `[NonUnique]`.

If `unique` is omitted, default is `true`.

Composite secondary key:

```yaml
keys:
  secondary:
    - fields: [Type, Rarity]
      unique: false
```

Generated:

```csharp
[SecondaryKey(0, keyOrder: 0), NonUnique]
public ItemType Type { get; init; }

[SecondaryKey(0, keyOrder: 1), NonUnique]
public Rarity Rarity { get; init; }
```

### 12.4 Key Field Types

Allowed key field types:

```text
int
long
string
generated enum
```

Disallowed key field types:

```text
bool
float
double
generated struct
list<T>
```

Rationale: key lookup and range behavior should avoid unstable or complex comparisons in v1.

### 12.5 Key Validation

Errors:

- Table has no primary key
- Key references an unknown field
- Key references a field with a disallowed key type
- Duplicate secondary key field sets
- Empty key field list
- Secondary key has duplicate field names
- Primary key has duplicate field names
- Duplicate primary key values in rows
- Duplicate unique secondary key values in rows

For composite keys, duplicate detection uses the tuple of field values in declared order.

## 13. Row Data

### 13.1 General Rules

Rows are written under `rows`.

Each row must provide every table field unless a default is explicitly supported by the field type.

v1 default behavior:

- Missing string field: error
- Missing scalar value field: error
- Missing enum field: error
- Missing struct field: error
- Missing list field: treated as empty `ImmutableArray<T>`

Unknown row properties are errors.

### 13.2 Scalar Values

YAML scalar values must match the declared type.

```yaml
Id: 1
Name: Potion
Enabled: true
Weight: 1.25
```

Numeric conversions must be lossless.

Examples:

- `1` may convert to `int` or `long`
- `1.25` may convert to `float` or `double`
- `1.25` must not convert to `int`
- `"1"` must not convert to `int` unless an explicit relaxed mode is added later

### 13.3 Enum Values

Rows use enum member names.

```yaml
Rarity: Rare
```

Numeric enum values in rows are errors in v1.

### 13.4 Struct Values

Struct values are YAML mappings.

```yaml
Price:
  Amount: 100
  Currency: gold
```

Rules:

- Missing struct fields are validated like table row fields.
- Unknown struct fields are errors.
- Nested structs are allowed if no cycle exists.

### 13.5 List Values

List values are YAML sequences.

```yaml
RewardIds: [10, 11]
Prices:
  - Amount: 100
    Currency: gold
  - Amount: 5
    Currency: gem
```

Null list values are errors in v1.

Empty list:

```yaml
RewardIds: []
```

## 14. Code Generation

### 14.1 Output Files

Generated C# files are written to `csharp.output`.

Each definition generates one file:

```text
dist/cs/
  ItemType.cs
  Rarity.cs
  Price.cs
  ItemMaster.cs
```

Generated file names are based on type names, not YAML file paths.

If two definitions would generate the same file path, validation fails.

### 14.2 Template Model

v1 templates are intentionally limited and separated by definition kind.

Supported placeholders:

```text
{{ namespace }}
{{ generator_version }}
{{ source_hash }}
{{ definition_name }}
{{ definition_kind }}
{body}
```

`{body}` is required exactly once.

The tool owns `{body}` completely. Users must not be able to edit attributes/properties by template because those affect binary compatibility.

### 14.3 Built-In Templates

Built-in table template:

```csharp
// <auto-generated />
// Generated by Lilja.MasterData {{ generator_version }}.
// Source hash: {{ source_hash }}
#nullable enable

using MasterMemory;
using MessagePack;
using System.Collections.Immutable;

namespace {{ namespace }};

{body}
```

Built-in struct template:

```csharp
// <auto-generated />
// Generated by Lilja.MasterData {{ generator_version }}.
// Source hash: {{ source_hash }}
#nullable enable

using MasterMemory;
using MessagePack;
using System.Collections.Immutable;

namespace {{ namespace }};

{body}
```

Built-in enum template:

```csharp
// <auto-generated />
// Generated by Lilja.MasterData {{ generator_version }}.
// Source hash: {{ source_hash }}
#nullable enable

namespace {{ namespace }};

{body}
```

Projects may use identical template files for all kinds, but the configuration supports separate files so enum, struct, and table output can differ without affecting binary-owned body generation.

### 14.4 Generated Body Rules

The body must include:

- `[MemoryTable("<table>")]` for table records
- `[MessagePackObject]` for tables and structs
- `[PrimaryKey]` for primary key fields
- `[SecondaryKey(indexNo, keyOrder)]` for secondary key fields
- `[NonUnique]` where configured
- `[Key(n)]` on all serialized fields
- `public sealed partial record` for table records
- `public readonly partial record struct` for custom structs
- `public enum` for enums
- MasterRef helper methods for table records, wrapped in `#if !LILJA_MASTERDATA_BUILD`

### 14.5 Field Attribute Ordering

Generated attributes should be ordered:

1. MasterMemory key attributes
2. `[NonUnique]` if present
3. `[Key(n)]`

Example:

```csharp
[SecondaryKey(0, keyOrder: 1), NonUnique]
[Key(3)]
public Rarity Rarity { get; init; }
```

## 15. C# Temporary Builder

### 15.1 Builder Project Contents

At build time, create:

```text
master-data/.lilja/temp/builder/
  Lilja.MasterData.GeneratedBuilder.csproj
  Program.cs
  Builder/
    MasterMemoryBinaryBuilder.cs
    BuildInput.cs
  Generated/
    ItemType.cs
    Price.cs
    ItemMaster.cs
```

The builder project references:

- MasterMemory
- MessagePack
- System.Collections.Immutable

Versions must be pinned by the orchestrator/build template.

### 15.2 Build Input JSON

The Rust orchestrator writes normalized JSON, not raw YAML, for the builder.

Example shape:

```json
{
  "namespace": "Game.MasterData",
  "outputPath": "dist/master-memory/master-data.bytes",
  "tables": [
    {
      "tableName": "items",
      "typeName": "ItemMaster",
      "fullTypeName": "Game.MasterData.ItemMaster",
      "fields": [
        { "name": "Id", "type": "int" },
        { "name": "Type", "type": "ItemType" },
        { "name": "RewardIds", "type": "list<int>" }
      ],
      "rows": [
        { "Id": 1, "Type": "Consumable", "RewardIds": [10, 11] }
      ]
    }
  ]
}
```

The exact JSON schema can evolve internally, but it must contain enough information to create objects without reading YAML.

### 15.3 Object Creation

For table records and structs, the builder creates instances without calling constructors:

```csharp
System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type)
```

If targeting a runtime where this API is unavailable, use:

```csharp
System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type)
```

Property assignment uses reflection:

```csharp
property.SetValue(instance, convertedValue);
```

The builder must fail if a generated property has no setter/init setter discoverable by reflection.

### 15.4 ImmutableArray Conversion

For `list<T>`, the builder must create `ImmutableArray<T>`.

The generic element type is known at runtime from the generated property type.

Implementation may use reflection against:

```csharp
System.Collections.Immutable.ImmutableArray.CreateRange<T>(IEnumerable<T>)
```

or a generic helper method closed with `MakeGenericMethod`.

### 15.5 Appending Tables

The builder uses MasterMemory dynamic append:

```csharp
builder.AppendDynamic(tableType, tableData);
```

`tableData` is an `IList` containing instances of `tableType`.

The builder must not require table-specific converter source generation.

## 16. Commands

### 16.1 `validate`

```bash
lilja-masterdata validate master-data
```

Performs:

- Config load
- YAML scan
- YAML parse
- Definition validation
- Row validation

Does not write generated C# or binary outputs.

May write non-source cache files under `.lilja/temp` if needed, but should avoid it.

### 16.2 `build`

```bash
lilja-masterdata build master-data
```

Performs full validation and generation.

Writes:

- `dist/cs`
- `.lilja/temp`
- `dist/master-memory/<fileName>`

Does not sync into Unity client.

### 16.3 `sync`

```bash
lilja-masterdata sync master-data
```

Copies generated outputs to configured sync destinations.

Before destructive updates, destination directories must contain:

```text
.lilja-master-data-generated
```

If the marker file is absent, the command fails unless an explicit initialization flag is used.

Suggested first-run command:

```bash
lilja-masterdata sync master-data --init
```

`--init` creates the destination directory and marker file if the directory does not exist or is empty.

### 16.4 `clean`

```bash
lilja-masterdata clean master-data
```

Removes generated outputs:

- `dist/cs`
- `dist/master-memory`
- `.lilja/temp`

It must not remove sync destinations in the Unity client.

### 16.5 `init`

Optional but recommended:

```bash
lilja-masterdata init master-data
```

Creates:

- `lilja-master-data.yaml`
- `master/`
- `templates/csharp/table.cs.tpl`
- `templates/csharp/struct.cs.tpl`
- `templates/csharp/enum.cs.tpl`
- `dist/cs/`
- `dist/master-memory/`
- sample enum/struct/table YAML files

## 17. Sync Semantics

`sync` copies generated artifacts after a successful `build`.

Default sync behavior:

- Clean files previously generated by Lilja.MasterData in destination directories.
- Copy all files from configured source directories.
- Preserve relative paths within each generated output directory.

Marker file:

```text
.lilja-master-data-generated
```

The marker file must include:

```text
This directory is managed by Lilja.MasterData.
Do not place hand-written files here.
```

The tool may maintain a manifest of copied files:

```text
.lilja-master-data-manifest.json
```

If manifest exists, delete only files listed in the previous manifest. If manifest does not exist, delete only if the directory contains the marker and no unknown files, otherwise fail.

## 18. Diagnostics

Diagnostics must include:

- Severity: error or warning
- Code: stable diagnostic code
- Message
- Source file path
- Line and column where available
- Definition kind/name where available
- Row identity where available
- Field name where available

Suggested code ranges:

```text
LMD0001-LMD0099 config errors
LMD0100-LMD0199 YAML parse errors
LMD0200-LMD0299 definition/schema errors
LMD0300-LMD0399 type resolution errors
LMD0400-LMD0499 row data errors
LMD0500-LMD0599 key/index errors
LMD0600-LMD0699 C# generation errors
LMD0700-LMD0799 temporary builder/dotnet errors
LMD0800-LMD0899 sync errors
LMD0900-LMD0999 MasterRef errors
```

Examples:

```text
LMD0402 error master/items.yaml:31:12 items row Id=1 field Price.Amount: expected int, got string
LMD0501 error master/items.yaml: keys.primary.fields[0]: unknown field "ItemId"
LMD0703 error dotnet SDK 8.0 or newer is required; found 7.0.400
LMD0904 error master/item-drops.yaml: refs[0] field ItemId -> ItemMaster.Id: referenced row does not exist
```

## 19. Determinism

The build must be deterministic.

Rules:

- Scan files in normalized lexical path order.
- Sort generated output writes by generated file path.
- Preserve field order from YAML.
- Preserve row order from YAML before MasterMemory build.
- Do not include wall-clock timestamps in generated files.
- Include generator version and source hash, not generation time.

`source_hash` should include:

- Config content
- All input YAML content
- Template content for table, struct, and enum templates
- Generator version

## 20. Dependencies

### 20.1 Runtime/Build Dependencies

The C# generated code and temporary builder require:

- MasterMemory
- MessagePack
- System.Collections.Immutable

Versions must be pinned in the temporary builder project template.

Unity client projects must also install compatible MasterMemory, MessagePack, and System.Collections.Immutable dependencies to compile generated C# and load the binary.

Lilja.MasterData v1 does not install these dependencies into Unity automatically.

### 20.2 Unity Compatibility Notes

Generated code uses:

- `record`
- `record struct`
- `init`
- `ImmutableArray<T>`

Unity client projects must use a Unity/C# environment that can compile these constructs or provide compatibility definitions such as `IsExternalInit` where needed.

If a target Unity version cannot compile `record struct`, the generator may later add a compatibility mode, but v1 default is immutable record generation.

## 21. Acceptance Tests

### 21.1 Basic Build

Given one enum, one struct, and one table:

- `validate` succeeds.
- `build` writes C# files for all definitions.
- `build` writes the MasterMemory binary.
- The temporary builder compiles.

### 21.2 Arbitrary Directory Layout

Given:

```text
master/battle/enemies.yaml
master/economy/items.yaml
master/types/rarity.yaml
```

Generation must be identical after moving files to:

```text
master/a.yaml
master/b.yaml
master/c.yaml
```

as long as YAML content is unchanged.

### 21.3 Immutable Generated Code

Generated table records use:

```csharp
public sealed partial record
```

Generated custom structs use:

```csharp
public readonly partial record struct
```

Generated properties use `init`, not `set`.

### 21.4 ImmutableArray

Given `type: list<int>`, generated C# uses:

```csharp
ImmutableArray<int>
```

The builder converts YAML sequences to `ImmutableArray<int>`.

### 21.5 Composite Primary Key

Given primary fields `[ItemId, Level]`:

- Generated properties include `[PrimaryKey(keyOrder: 0)]` and `[PrimaryKey(keyOrder: 1)]`.
- Duplicate `(ItemId, Level)` tuples fail validation.

### 21.6 Secondary Keys

Given a unique secondary key:

- Generated code does not include `[NonUnique]`.
- Duplicate key values fail validation.

Given a non-unique secondary key:

- Generated code includes `[NonUnique]`.
- Duplicate key values are accepted.

### 21.7 Invalid Rows

Validation fails for:

- Unknown field
- Missing required field
- Wrong scalar type
- Unknown enum member
- Struct object missing required field
- Non-sequence value for `list<T>`

### 21.8 Sync Safety

`sync` fails if destination exists and marker file is missing.

`sync --init` creates marker files for empty or missing destinations.

`clean` never deletes Unity client sync destinations.

## 22. Open Decisions

These are intentionally not decided for v1 implementation unless needed later:

- Whether to support nullable fields
- Whether to support default values in schema
- Whether to support non-unique primary keys
- Whether to support table reference validation
- Whether to support path-preserving generated C# output
- Whether to support compatibility mode without `record struct`
- Whether to ship a UPM package containing only samples/docs
- Whether to sync source YAML into the Unity project for debug/editor use
