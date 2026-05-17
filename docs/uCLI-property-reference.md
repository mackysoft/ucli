> [!IMPORTANT]
> この文書は、uCLI の公開 CLI 出力契約、内部 IPC 契約、`payload`、status、readIndex catalog のプロパティ定義をまとめたリファレンスである。
> 全体契約は [uCLI.md](uCLI.md)、コマンドの option table と実行例は [uCLI-command-reference.md](uCLI-command-reference.md)、JSON リクエスト入力契約は [json-request-spec.md](json-request-spec.md) を参照する。

## 公開 CLI 出力契約

公開 CLI 出力の種別と意味は [uCLI.md](uCLI.md) を正本とする。ここでは `request-response` 型で使う JSON shape だけを定義する。
公開 CLI JSON の固定対象は専用 JSON writer、command output DTO、Golden files であり、この文書は property の意味と参照先を説明する。
公開 CLI payload schema は command output DTO、専用 JSON writer、Golden files から生成される contract artifact であり、この文書を手書き schema の正本にはしない。JSON Schema は構造検証を担い、verdict 再計算、参照解決、required claim 整合などの cross-field invariant は semantic invariant validator と Golden tests で検証する。

### request-response 型の共通エンベロープ
`request-response` 型の CLI JSON 出力は、次の共通エンベロープを返す。

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `protocolVersion` | integer | yes | プロトコルメジャーバージョン |
| `command` | string | yes | コマンド識別子。例: `test.run` |
| `status` | `ok \| error` | yes | 実行成否 |
| `exitCode` | integer | yes | プロセス終了コード |
| `message` | string | yes | 人間向け説明 |
| `payload` | object | yes | コマンド固有結果 |
| `errors` | array | yes | CLI エラー配列。正常時は空配列 |

`requestId` は CLI 共通エンベロープには含めない。必要な場合だけ各コマンドの `payload` に含める。

### Payload schema family

公開 CLI JSON は `Envelope<TSuccessPayload,TErrorPayload>` として schema 化する。top-level の `command` が payload schema を決める discriminator であり、`payload` 自体に payload kind field は持たせない。

- `status=ok` の payload は command-specific success payload schema に一致する。
- `status=error` の payload は command-specific error payload schema に一致する。
- `status=ok` のとき `errors=[]`、`status=error` のとき `errors.length >= 1` とする。
- error payload を持たない command は空 object を返す。startup diagnosis など、失敗時にも構造化情報を返す command だけが専用 error payload schema を持つ。

payload schema は common envelope、common component、command payload、semantic invariant validator に分ける。request command payload schema は `project`、`requestId`、`opResults[]`、`readIndex`、`planToken`、`readPostcondition`、`contractViolations[]` などの command result envelope を検証する。`opResults[].result` の中身は `ucli ops describe` が返す operation `resultSchema` に委譲し、command payload schema には複製しない。

assurance command payload schema は `verdict`、`project`、`verifiers[]`、`claims[]`、`reports`、`residualRisks[]` を共通必須 component とする。`ready`、`compile`、`verify` はこの共通 component に command-specific field を加える。

公開 payload schema は原則 closed schema とする。未知 field を受け入れる拡張は、`protocolVersion` の更新または明示的な extension field で扱う。open code set は schema enum にせず、string shape と pattern だけを検証し、静的意味は `ucli codes describe <CODE>` で解決する。

### `payload.project`

Unity IPC 実行結果や assurance packet など、後続検証で project 同一性が必要な command payload は、実行対象の project identity を `payload.project` に返す。`verify --from` はこの identity を使って、過去の mutation result と現在解決した project が同一であることを検証する。

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `projectPath` | string | yes | 正規化済み Unity project root absolute path |
| `projectFingerprint` | string | yes | worktree local storage root と Unity project path から算出した fingerprint |
| `unityVersion` | string | yes | `ProjectSettings/ProjectVersion.txt` から解決した Unity version。取得不能な command では `unknown` |

### CLI エラーオブジェクト

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `code` | string | yes | 機械判定用エラーコード |
| `message` | string | yes | 説明 |
| `opId` | `string \| null` | yes | 該当実行単位の `id`。該当なしは `null` |

`code` は open code set である。既知コード一覧にない値でも JSON 契約上は有効であり、利用側は未知値を汎用失敗として扱う。C# 契約では機械判定用エラーコードを `UcliErrorCode` で扱い、既知コードは責務別の typed code definition として定義する。JSON wire shape は文字列のままとする。

内部の失敗分類は CLI エンベロープへ投影される診断モデルであり、CLI エラーオブジェクトの共通 field は追加しない。利用側は引き続き `status`、`exitCode`、`message`、`errors[].code`、`errors[].message`、`errors[].opId` だけを読めばよい。

### Command failure と verifier failure

`status=error` は command 自体が成立しなかったことを表す。例として、引数不正、project 解決失敗、IPC timeout、transport failure、profile parse failure、検証 artifact 破損が該当する。

verifier が正常に実行され、その結果として検証対象の不成立を確認した場合は command failure ではない。この場合、`status=ok` のまま、command-specific payload の `verdict=fail`、`claims[].status=failed`、非 0 `exitCode` を返してよい。

利用側は次の順に判定する。

1. `status` と `errors[]` で command failure を判定する。
2. `payload.verdict` と `claims[]` で verifier outcome を判定する。
3. `claims[].coverage`、`claims[].residualRisks[]`、`payload.residualRisks[]` で保証不足を判定する。

`ready`、`compile`、`verify` の assurance command は `payload.verdict` を必ず返す。`payload.verdict=pass` は必須 claim がすべて `passed`、coverage が `full`、claim-local と payload-global のどちらにも `blocking=true` の residual risk が無い場合だけ返す。`payload.verdict=fail` は verifier が正常実行され、必須 claim の不成立を確認した場合に返す。`payload.verdict=incomplete` は必須 claim に `partial`、`indeterminate`、`unverified`、`outOfScope`、または `coverage=none` が残る場合に返す。

| Envelope / verdict | Exit code |
| --- | --- |
| `status=ok`, `payload.verdict=pass` | `0` |
| `status=ok`, `payload.verdict=fail` | `1` |
| `status=ok`, `payload.verdict=incomplete` | `1` |
| `status=error` | failure kind に従う |

### Assurance claim payloads

`ready`、`compile`、`verify` などの assurance command は、command-specific payload に共通 shape として `verdict`、`project`、`verifiers[]`、`claims[]`、`reports`、`residualRisks[]` を必ず含める。`claims[]` は summary ではなく、保証判断に使う machine-readable claim packet である。`ready` で artifact が無い場合でも `reports` は空 object として返す。

#### `payload.verdict`

| Value | Meaning |
| --- | --- |
| `pass` | 必須 claim がすべて成立し、coverage が full で、claim-local と payload-global のどちらにも `blocking=true` の residual risk がない |
| `fail` | verifier が正常実行され、必須 claim の不成立を確認した |
| `incomplete` | verifier は成立したが、partial / indeterminate / unverified / outOfScope / coverage none が残る |

#### `payload.claims[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `id` | string | yes | claim code。例: `UNITY_COMPILE_NO_ERRORS`。`codes` 台帳の対象であり、種別を問わず uCLI 全体で一意 |
| `required` | boolean | yes | `payload.verdict` の pass 判定に必須の claim か |
| `verifierRef` | string | yes | claim を生成した verifier の stable id。`payload.verifiers[].id` と対応する |
| `status` | `passed \| failed \| partial \| indeterminate \| unverified \| outOfScope` | yes | claim の成立状態 |
| `subject` | object | no | claim 対象。project、scene、prefab、asset、read surface など |
| `validity` | object | no | claim の有効範囲。probe-only readiness など、再利用可能性に制約がある場合に返す |
| `statement` | string | yes | claim の主張 |
| `evidence` | array | yes | claim を支える evidence の配列。空配列は不可 |
| `coverage` | `full \| partial \| none` | yes | claim の検証 coverage |
| `residualRisks` | array | yes | claim に残る残余リスク。無い場合は空配列 |

`claims[].status` は command `status` と独立して読む。`claims[].status=failed`、`partial`、`indeterminate`、`unverified` は、command envelope が `status=ok` でも発生し得る。`payload.verdict` の唯一の判定入力は `claims[].required`、`claims[].status`、`claims[].coverage`、`claims[].residualRisks[].blocking`、`payload.residualRisks[].blocking` である。consumer はこれらの field から `payload.verdict` を再計算できなければならない。`claims[].verifierRef` は同じ payload の `payload.verifiers[].id` に必ず存在しなければならない。`claims[].required=true` の claim は、`required=true` の verifier を参照しなければならない。

#### `claim.evidence[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `kind` | string | yes | evidence kind。例: `lifecycleSnapshot`、`compileSummary`、`testSummary`、`operationResult`、`postRead` |
| `evidenceRef` | string | no | `payload.reports` への参照 key |
| `data` | object | no | 小さい evidence 値。大きい artifact は `evidenceRef` と `reports` を使う |

`evidenceRef` は claim から report / artifact source を参照するための stable key である。file path、artifact digest、step id などの詳細は `payload.reports` 側に置く。各 evidence item は `evidenceRef` または `data` の少なくとも一方を必ず持つ。小さい evidence 値や同一 payload 内の値は `data` に直接入れる。`kind` だけの evidence item は invalid とする。

#### `claim.validity`

`claim.validity` は claim が成立する時間的・session 的範囲を表す。`ready --mode auto` が oneshot readiness probe に解決された場合、ready claim は probe session の成立だけを保証し、後続 command が同じ Unity process を使うことは保証しない。

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `kind` | `sessionBound \| probeOnly \| artifactBound` | yes | claim の有効範囲 |
| `guaranteesReusableSession` | boolean | yes | 後続 command が同じ Unity session を再利用できる保証がある場合は `true` |

ready claim では `validity` を必須とする。その他の claim では、時間的・session 的・artifact 的な制約がある場合にだけ `validity` を返す。

#### `payload.residualRisks[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `code` | string | yes | risk code。`codes` 台帳の対象。code value は種別を問わず uCLI 全体で一意 |
| `message` | string | yes | 残余リスクの説明 |
| `claimId` | string | no | 関連する claim code |
| `blocking` | boolean | yes | `true` の場合、この risk は `payload.verdict=pass` を阻害する |

`claims[].residualRisks[]` は claim 固有の residual risk を返す。`payload.residualRisks[]` は payload 全体に固有の global residual risk だけを返し、claim-level risk の重複集約には使わない。`payload.verdict=pass` は、`claims[].residualRisks[].blocking` と `payload.residualRisks[].blocking` のどちらにも `true` が無い場合だけ成立する。`blocking` は必須であり、省略時の既定値は定義しない。

`payload.verdict=pass` は Unity-local assurance command の判定であり、外部 supervisor の reviewless green 十分条件ではない。外部 supervisor は task intent、外部静的解析、scenario profile、dangerous operation、`probeOnly` validity、`outOfScope` / `unverified` claim、non-blocking residual risk の扱いを別途判定する。
`blocking=false` の residual risk は uCLI-local pass を阻害しないが、外部 supervisor の review policy input として扱う。

#### `payload.reports`

`reports` は verifier や evidence source が残した artifact への参照 table である。各 property name は `evidenceRef` から参照される stable key とする。

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `kind` | string | yes | artifact kind。例: `compileSummary`、`testSummary`、`unityLog` |
| `path` | string | no | artifact path |
| `digest` | string | no | artifact 内容の digest。形式は `sha256:<hex>`。locator ではなく integrity metadata |
| `uri` | string | no | file path 以外の locator |

各 `payload.reports[ref]` は、`kind` に加えて `path` または `uri` の少なくとも 1 つを持たなければならない。`digest` は integrity metadata であり、単独では evidence source の locator にならない。空 object と digest-only entry は invalid とする。小さい evidence 値は `claim.evidence[].data` に直接置く。v1 は `inlineDataRef` や payload-wide inline data table を定義しない。`evidence[].evidenceRef` は `payload.reports` の key に解決できなければならない。`payload.verifiers[].reportRef` が存在する場合は、同じ key が `payload.reports` に存在しなければならない。

#### `payload.profile`

`verify` は実効 verify profile の identity と digest を `payload.profile` に返す。

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `source` | `builtIn \| file` | yes | profile の取得元 |
| `name` | string | yes | built-in profile 名、または user profile の logical name。名称が無い file profile は normalized path 由来の stable name を返す |
| `path` | `string \| null` | yes | file profile の repository-relative path。built-in profile では `null` |
| `digest` | string | yes | profile identity と effective steps を含む canonical digest。形式は `sha256:<hex>` |

profile digest の入力は、`source`、`name`、`path`、canonicalized effective steps、verifier-specific args を含む。`built-in:default` と `built-in:project` が同じ verifier set を持つ場合でも、profile 名が異なるため digest は異なる。profile 変更は verify surface の変更として扱う。

#### `payload.profileDigest`

`payload.profileDigest` は `payload.profile.digest` と同じ値を返す短縮 projection である。consumer は `payload.profile` を正本として読む。

#### `payload.verifiers[]`

assurance command は実行した verifier を `payload.verifiers[]` に返す。これは profile の要約ではなく、実際に実行された verifier と、その検証に伴う Unity 状態遷移の evidence index である。`payload.verifiers[]` が verifier の正本であり、claim は `claims[].verifierRef` で参照する。

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `id` | string | yes | verifier stable id。claim の `verifierRef` から参照される |
| `kind` | string | yes | verifier kind。例: `ready`、`compile`、`postRead`、`test`、`logs` |
| `deterministic` | boolean | yes | 同じ入力と同じ観測境界で決定論的に再実行できる場合は `true` |
| `required` | boolean | yes | `payload.verdict` の pass 判定に必須か |
| `primaryClaims` | string[] | yes | この verifier の primary claim code 一覧 |
| `effects` | string[] | yes | verifier が起こし得る副作用。副作用が無い場合は空配列 |
| `reportRef` | string | no | `payload.reports` の参照 key |

`ready` command は runtime lifecycle を観測した場合は `id=ready.lifecycle`、readIndex artifact を観測した場合は `id=ready.readIndex` の verifier を返す。`compile` command は少なくとも `id=compile` の verifier を返し、その `effects` は `assetDatabaseRefresh`、`scriptCompilation`、`domainReload` を含む。`verify` command は実効 profile から実行した verifier を canonical order で返す。`required=true` の verifier は、少なくとも 1 つの `required=true` primary claim を生成しなければならない。`payload.verifiers[].primaryClaims[]` の各 code は `claims[].id` に存在し、その claim は同じ verifier を `verifierRef` で参照しなければならない。`required=true` の verifier が primary claim を生成できない場合は、該当 primary claim を `status=indeterminate` または `unverified`、`required=true` として返し、`payload.verdict=incomplete` とする。

#### `payload.compile`

`compile` command の payload は `compile` object を返し、AssetDatabase refresh、script compilation、domain reload の観測境界を分ける。

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `refresh` | object | yes | AssetDatabase refresh の実施有無、開始時刻、完了時刻 |
| `scriptCompilation` | object | yes | compile 開始・完了状態、diagnostic counts |
| `domainReload` | object | yes | reload の必要性、観測有無、generation before / after |

`UNITY_DOMAIN_RELOAD_SETTLED` claim は reload が発生したことではなく、compile 後の domain reload state が settled であることを表す。reload が不要だった場合は `domainReload.reloadRequired=false`、`reloadObserved=false`、generation before / after が同一の evidence を返せる。

#### `postRead` claims

`verify --from <ucli-result.json>` の `postRead` verifier は、`readPostcondition` だけでなく `opResults[].applied`、`changed`、`touched`、Play Mode 変更かどうか、commit 種別を読んで claim を生成する。`--from` の `payload.project.projectFingerprint` と現在解決した project fingerprint が一致しない場合は `PROJECT_FINGERPRINT_MISMATCH` の command failure とし、postRead claim を生成しない。

`--from` input を読めない、または v1 の verify input として扱えない場合は command failure とする。標準 code は `VERIFY_INPUT_SCHEMA_UNSUPPORTED`、`VERIFY_INPUT_PROTOCOL_VERSION_MISMATCH`、`VERIFY_INPUT_COMMAND_UNSUPPORTED`、`VERIFY_INPUT_PAYLOAD_INVALID`、`VERIFY_INPUT_PROJECT_MISSING`、`PROJECT_FINGERPRINT_MISMATCH` とする。

`verify --from` は `opResults[].diagnostics[]` も読む。`coverageImpact=partial` または `indeterminate` の diagnostic は、postRead verifier が partial / indeterminate claim または blocking residual risk へ写す。request command の diagnostic を verify flow から無視してはならない。

| Claim kind | Meaning |
| --- | --- |
| `PERSISTENCE_UNIT_TOUCHED` | `opResults[].touched` に基づき、永続化単位の影響境界を確認した |
| `READ_SURFACE_SAFE` | `readPostcondition.requirements[]` に基づき、readIndex または live read の安全条件を確認した |
| `POST_MUTATION_OBSERVED` | 変更後の対象を再読込し、期待する post-mutation state を観測した |

`PERSISTENCE_UNIT_TOUCHED` は、`changed=true` かつ永続化単位が期待される mutation では `required=true` とする。Play Mode live object 変更のように永続化単位を期待しない mutation では `outOfScope` または `required=false` とする。`READ_SURFACE_SAFE` は `readPostcondition.requirements[]` が存在する場合に `required=true` とする。`POST_MUTATION_OBSERVED` は edit DSL など、request から期待 post-state を決定論的に導ける場合だけ `required=true` とする。raw op や broad mutation など期待 post-state が定義されない場合は `outOfScope` または `required=false` とし、実装ごとの推測で required claim にしてはならない。

## 内部 IPC 契約

### `IpcResponse`
CLI と Unity runtime の間では、共通 CLI エンベロープではなく次の IPC エンベロープを使用する。

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `protocolVersion` | integer | yes | IPC プロトコルメジャーバージョン |
| `requestId` | string | yes | IPC リクエスト相関 ID |
| `status` | `ok \| error` | yes | IPC 実行成否 |
| `payload` | object | yes | IPC method-specific payload |
| `errors` | array | yes | IPC エラー配列。正常時は空配列 |

ここでの `status` は IPC 層の成否である。

### Execute payload
公開 CLI の request 系 command は、この構造を `payload` として返す。内部 IPC では同じ構造を `IpcExecuteResponse` として扱う。

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `project` | object | yes | 実行対象 project identity。shape は `payload.project` を参照 |
| `opResults` | array | yes | 各 public step の結果 |
| `contractViolations` | array | no | `OPERATION_CONTRACT_VIOLATION` の詳細。shape は `payload.contractViolations[]` を参照 |
| `planToken` | string | no | `plan` 実行時に発行されたトークン。未発行時は field 自体を省略する |
| `readPostcondition` | object | no | mutation 後 read の safe 条件。shape は `IpcExecuteResponse.readPostcondition` を参照 |

### `IpcExecuteResponse.opResults[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `opId` | string | yes | 対応する実行単位の `id` |
| `op` | string | yes | 実行単位名。`kind:"edit"` step では `"edit"` を返す |
| `phase` | `validate \| plan \| call \| skipped` | yes | 実行フェーズ |
| `applied` | boolean | yes | step 内のどこかが適用済みか |
| `changed` | boolean | yes | step 内のどこかに実変更があったか |
| `touched` | array | yes | 影響した永続化単位の配列 |
| `diagnostics` | array | yes | step 固有の診断配列。無い場合は空配列 |
| `result` | object | no | `kind:"op"` step の operation 固有結果本体。`edit` と `UcliNoResult` operation では返さない |

`result` は operation ごとの Result contract 型に対応する。`phase`、`applied`、`changed`、`touched`、`diagnostics`、`errors` は envelope 側の情報であり、`result` の中には含めない。

#### `IpcExecuteResponse.opResults[].diagnostics[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `code` | string | yes | diagnostic code。`codes` 台帳の対象 |
| `severity` | `info \| warning \| error` | yes | 診断 severity |
| `coverageImpact` | `none \| partial \| indeterminate` | yes | この診断が対象 step の coverage へ与える影響 |
| `message` | string | yes | 診断説明 |
| `applicationState` | `notApplied \| applied \| indeterminate \| unknown` | no | 失敗診断が適用状態の情報を持つ場合だけ返す。`notApplied` は未適用を断定できる場合だけ使う |

`opResults[].diagnostics[]` は、request response 内で silent clean pass にしてはいけない観測不足を返す場所である。例として、`hierarchyPath` で表現できない GameObject 名を candidate から除外した場合は `HIERARCHY_PATH_UNREPRESENTABLE_OBJECTS`、`severity=warning`、`coverageImpact=partial` を返す。

`opResults[].diagnostics[]` は request command の `status` を自動的には変更しない。`severity=error` は、その step の operation failure または preflight failure として扱う場合を除き、diagnostic として返してはならない。`coverageImpact=partial` または `indeterminate` の diagnostic は、後続の `verify --from` で partial / indeterminate claim または residual risk へ反映する。

runtime result が operation metadata の `assurance` facts に反した場合は `OPERATION_CONTRACT_VIOLATION` とする。公開 CLI は `status=error`、`errors[].code=OPERATION_CONTRACT_VIOLATION`、`errors[].opId=<該当 step id>` を返す。operation envelope を信頼できる形で観測済みの場合は `payload.opResults[]` に該当 step の envelope を残す。信頼できる operation envelope が生成されていない場合は `payload.opResults: []` とする。`payload.contractViolations[]` には期待 fact、観測 result、適用状態を返す。同じ静的意味を持つ `OPERATION_CONTRACT_VIOLATION` diagnostic を返す場合も、`coverageImpact=indeterminate` と `applicationState` を付ける。このエラーは operation 実装または metadata の不整合であり、未適用を意味しない。

#### `payload.contractViolations[]`

内部 IPC では同じ配列を `IpcExecuteResponse.contractViolations[]` として扱う。

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `opId` | string | yes | 矛盾が検出された step id |
| `operation` | string | yes | 矛盾が検出された operation 名。`kind:"edit"` lowering の場合は実行 primitive 名 |
| `expectedFact` | string | yes | `ops describe` 由来の期待 fact。例: `assurance.mayDirty=false` |
| `observedResult` | string | yes | 実行結果から観測した矛盾内容。例: `opResults[].changed=true` |
| `applicationState` | `notApplied \| applied \| indeterminate \| unknown` | yes | 矛盾検出時点の適用状態。`notApplied` は未適用を断定できる場合だけ使う |

`status=error` かつ `errors[].code=OPERATION_CONTRACT_VIOLATION` の場合、`contractViolations[]` は 1 件以上でなければならない。`applicationState=notApplied` 以外は再試行安全性を保証しない。

`OPERATION_CONTRACT_VIOLATION` の public error payload は次の shape を golden とする。

```json
{
  "status": "error",
  "errors": [
    {
      "code": "OPERATION_CONTRACT_VIOLATION",
      "message": "Operation result violated declared assurance facts.",
      "opId": "step1"
    }
  ],
  "payload": {
    "contractViolations": [
      {
        "opId": "step1",
        "operation": "ucli.some.operation",
        "expectedFact": "assurance.mayDirty=false",
        "observedResult": "opResults[].changed=true",
        "applicationState": "indeterminate"
      }
    ],
    "opResults": []
  }
}
```

#### `IpcExecuteResponse.opResults[].touched[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `kind` | `scene \| prefab \| asset \| projectSettings` | yes | 永続化単位の種別 |
| `path` | string | yes | プロジェクトルート相対パス |
| `guid` | string | no | 取得可能な場合に付与する GUID |

#### Play Mode 変更時の `IpcExecuteResponse.opResults[]`
`--allowPlayMode` 付きの Play Mode 変更では、Play Mode の live object を変更対象とする。

- `applied` / `changed` は live object への適用結果を表す。
- Scene context の `touched` は永続化単位を表さないため、Prefab apply / revert を含まない場合は空配列を返す。
- `changed = true` かつ `touched = []` は、Play Mode の live object は変わったが永続化単位は保存していない状態を表す。
- Scene context では Prefab apply / revert を含まない場合、`readPostcondition` は返さない。
- Scene context の `applyPrefabOverrides` で Prefab asset へ反映した場合は、その Prefab asset を `touched` に返し、必要な `readPostcondition` を返す。
- Scene context の `revertPrefabOverrides` は Scene live object だけを戻すため、`touched` は空配列、`readPostcondition` は返さない。
- Prefab apply / revert の preflight 検証エラーでは action 全体を適用せず、成功扱いの `touched` / `readPostcondition` は返さない。
- Prefab apply / revert の Unity API 実行後に失敗した場合は失敗診断を返し、成功扱いの `touched` / `readPostcondition` は返さない。
- Prefab / asset / project context の保存を伴う変更では、通常の永続化変更と同じく保存した永続化単位を `touched` に返し、必要な `readPostcondition` を返す。
- `result` は通常の `edit` step と同じく省略する。
- `--readIndexMode` 未指定時の `plan` の `payload.readIndex` は `used=false`、`source=unity`、`fallbackReason="Play Mode mutation uses live Unity state."` を返す。

#### `IpcExecuteResponse.readPostcondition`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `requirements` | array | yes | stale 化した read surface ごとの safe 条件 |

#### `IpcExecuteResponse.readPostcondition.requirements[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `surface` | `assetSearch \| guidPath \| sceneTreeLite` | yes | 対象 read surface |
| `scenePath` | string | no | `surface = sceneTreeLite` のときだけ使用する。指定時は normalize 済み scene path、未指定時は全 Scene が対象 |
| `minSafeGeneratedAtUtc` | string | yes | この時刻以降に生成された readIndex だけを safe とみなす UTC 時刻 |

matching requirement がある場合、safe 判定は `payload.readIndex.generatedAtUtc >= minSafeGeneratedAtUtc` を満たすか、または live Unity 再観測であることを要求する。`generatedAtUtc` 欠落または requirement より古い readIndex は unsafe であり、live fallback が必要である。matching requirement が無い surface にだけ既存の `freshness` 契約を適用する。

#### `IpcExecuteResponse.opResults[].result` for `ucli.assets.find`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `matches` | array | yes | `assetPath` の ordinal 昇順で並ぶ一致結果。未一致時は空配列 |
| `window` | object | yes | bounded window 情報。`limit`、`cursor`、`nextCursor`、`isComplete`、`totalCount` を返す |

`ucli.assets.find` は raw op でも `limit` と `cursor` を受け付ける。既定 `limit=100`、最大 `10000` とし、明示 opt-in なしに全件を stdout payload へ返さない。`ucli.assets.find` の `touched` は常に空配列である。`window.totalCount` は同じ filter 条件で cursor を使わず全 window を走査した場合の一致 asset 数であり、count を計算していない、または bounded computation policy により算出しない場合は `null` とする。`window.isComplete` は現在 window が最後まで到達したかを表し、`totalCount` の有無とは独立である。

#### `IpcExecuteResponse.opResults[].result` for `ucli.scene.tree`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `path` | string | yes | 対象 scene asset path |
| `roots` | array | yes | bounded window 内の root GameObject tree |
| `sourceState` | object | yes | 読み取り元。`temporaryScene`、`loadedScene`、`persistedPreview`、`readIndex` のいずれか |
| `window` | object | yes | bounded window 情報。`limit`、`cursor`、`nextCursor`、`isComplete`、`totalCount` を返す |

`ucli.scene.tree` は raw op でも `limit` と `cursor` を受け付ける。既定 `limit=100`、最大 `10000` とし、明示 opt-in なしに全件を stdout payload へ返さない。階層走査順は deterministic な hierarchy traversal order とし、`window.cursor` はその順序に対する cursor とする。`window.totalCount` は同じ scene、source、depth 条件で cursor を使わず全 window を走査した場合の traversal node 数であり、count を計算していない、または bounded computation policy により算出しない場合は `null` とする。`window.isComplete` は現在 window が最後まで到達したかを表し、`totalCount` の有無とは独立である。

##### `IpcExecuteResponse.opResults[].result.roots[]` for `ucli.scene.tree`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `name` | string | yes | GameObject 名 |
| `globalObjectId` | string | yes | 解決できたときの GlobalObjectId。解決不能時は空文字列 |
| `children` | array | yes | 現在 window で返した子 node 一覧 |
| `childrenState` | `complete \| notExpandedByDepth \| truncatedByWindow \| unknown` | yes | `children` が完全か、省略または切り詰めを含むか |

`childrenState=complete` は子をすべて返したことを表す。`notExpandedByDepth` は `depth` により子を展開していない状態、`truncatedByWindow` は `limit` により traversal の途中で切った状態、`unknown` は読み取り元の制約により完全性を判定できない状態である。`children: []` だけでは「子が無い」と「展開していない」を区別しないため、consumer は必ず `childrenState` を読む。

#### `IpcExecuteResponse.opResults[].result` for `ucli.resolve`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `globalObjectId` | string | yes | 解決済み GlobalObjectId |

`ucli.resolve` の `touched` は常に空配列である。

##### `IpcExecuteResponse.opResults[].result.matches[]` for `ucli.assets.find`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `assetPath` | string | yes | `Assets/` から始まる persistent main asset path |
| `assetGuid` | string | yes | `AssetDatabase.AssetPathToGUID(assetPath)` の結果 |
| `name` | string | yes | main asset 名 |
| `typeId` | string | yes | main asset runtime type の stable `typeId` |

### `IpcError`
現在の `IpcError` は CLI の `errors[]` と同じ field shape を持つが、契約の層は別である。

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `code` | string | yes | 機械判定用エラーコード |
| `message` | string | yes | 説明 |
| `opId` | `string \| null` | yes | 該当実行単位の `id`。該当なしは `null` |

`code` は CLI `errors[]` と同じ open code set の値を文字列として運ぶ。IPC 受信側は未知値を破棄せず保持する。

### Code catalog payloads
`ucli codes` は、公開 JSON 契約に現れる open code set の正本台帳である。code value は error、diagnostic、reasonCode、claim、risk の種別を問わず uCLI 全体で一意とする。`kind` は分類と `list` filter のための metadata であり、code identity ではない。同じ code を複数 field に出してよいのは、どの field でも静的意味が完全に同じ場合だけである。uCLI 定義の code は uppercase snake case を基本形とし、claim の読みやすい主張は `claims[].statement` に置く。

codes 対象は `errors[].code`、`diagnosis.primaryDiagnostic.code`、`opResults[].diagnostics[].code`、`claims[].id`、`residualRisks[].code`、明示的な `reasonCode` field に限定する。`lifecycleState`、`blockingReason`、`startupBlockingReason`、`retryDisposition`、`diagnosis.reason`、`fallbackReason` のような lowerCamel の分類値は code ではない。v1 では `reason` kind は予約済みであり、標準 payload は `reasonCode` field を emit しない。将来 `reasonCode` を追加する場合は、その field path を `codes describe` の `appearsIn[]` に固定する。

uCLI の runtime JSON は bare `code` を返す。外部 supervisor が uCLI と他ツールの report を束ねる場合、uCLI code の tool 横断 identity は `ucli:<CODE>` として扱う。`ucli:` prefix は aggregation layer の identity であり、uCLI の JSON field value には埋め込まない。

#### `ucli codes list`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `catalogVersion` | integer | yes | 台帳 payload の schema version |
| `source` | string | yes | 台帳の取得元。bundled 定義は `bundled` |
| `kinds` | string[] | yes | 台帳が扱う kind 一覧 |
| `codes` | array | yes | 既知 code の一覧。filter 後に該当が無い場合は空配列 |

#### `ucli codes list payload.codes[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `code` | string | yes | uCLI 全体で一意な code value |
| `kind` | string | yes | code kind。例: `error`、`claim`、`risk` |
| `category` | string | yes | kind 内での分類 |
| `summary` | string | yes | 一覧上で code を選ぶための短い要約 |

#### `ucli codes describe`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `code` | string | yes | 説明対象の code。uCLI 全体で一意 |
| `known` | boolean | yes | 現在の uCLI client が台帳定義を持つ場合は `true` |
| `kind` | string | yes | code の種別。identity ではない。未知 code は `unknown` |
| `category` | string | yes | code の分類。未知 code は `unknown` |
| `summary` | string | yes | 1行の要約 |
| `meaning` | string | no | 既知 code の静的意味。未知 code では省略してよい |
| `appearsIn` | string[] | yes | code が現れる JSON field |
| `appliesTo` | string[] | no | 関連する CLI command 名 |
| `coverageImpact` | object | no | coverage や completeness へ与える静的影響 |
| `verdictSemantics` | object | no | pass / fail / indeterminate 判定への静的影響 |
| `executionSemantics` | object | no | 適用状態と再試行判断の既定解釈。主に `error` kind で使う |
| `inspect` | string[] | no | 次に読むべき field または補助コマンド |
| `relatedCodes` | string[] | no | 近接する code value |

`codes describe` は run 固有の結論を返さない。実際の失敗 JSON を読んだ説明は別 command の責務であり、静的台帳は code value の意味だけを返す。`codes describe <KIND:CODE>` は期待 kind の検証付き alias であり、code が存在しても kind が一致しない場合は `INVALID_ARGUMENT` を返す。

## lifecycle 関連プロパティ

### lifecycle status フィールド
`ucli status` と `ucli daemon status` は、必要に応じて次の lifecycle 関連 field を `payload` に含める。各コマンドが返す field の subset は、コマンド別 `payload` 契約を正とする。

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `daemonStatus` | `running \| notRunning \| stale` | yes | daemon 到達性と session 状態 |
| `unityVersion` | `string \| null` | yes | `ProjectSettings/ProjectVersion.txt` 由来の Unity バージョン |
| `serverVersion` | `string \| null` | yes | `daemonStatus = running` のときのみ値を持つ |
| `editorMode` | `batchmode \| gui \| null` | yes | `daemonStatus = running` のときのみ値を持つ。`gui` は Unity GUI Editor 内の uCLI endpoint を表す |
| `lifecycleState` | `starting \| ready \| busy \| compiling \| domainReloading \| playmode \| blockedByModal \| safeMode \| shuttingDown \| null` | yes | `daemonStatus = running` のときのみ値を持つ。batchmode Editor session は `starting / ready / busy / compiling / domainReloading / playmode / shuttingDown` の subset を返し、GUI Editor session は `blockedByModal` と `safeMode` も返す |
| `blockingReason` | `null \| startup \| busy \| compile \| domainReload \| playMode \| modalDialog \| safeMode \| shutdown` | yes | 実行を止めている理由。batchmode Editor session は `startup / busy / compile / domainReload / playMode / shutdown` の subset を返し、GUI Editor session は `modalDialog` と `safeMode` も返す |
| `compileState` | `ready \| compiling \| null` | yes | compiler activity 専用の状態 |
| `compileGeneration` | `string \| null` | yes | compile 開始または完了ごとに変化する opaque な識別子 |
| `domainReloadGeneration` | `string \| null` | yes | domain reload 完了ごとに変化する opaque な識別子 |
| `canAcceptExecutionRequests` | boolean | yes | 通常実行要求を受け付けられるときのみ `true`。`--allowPlayMode` の例外可否は含めず、`lifecycleState = playmode` では `false` |
| `timeoutMilliseconds` | integer | yes | 実効タイムアウト |

## コマンド別 `payload`

### `ucli daemon`
`ucli daemon` はサブコマンドにかかわらず `payload.timeoutMilliseconds` を常に含む。

#### `daemon start`

成功時の payload は endpoint と session 登録が完了した session snapshot を返す。

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `startStatus` | `started \| alreadyRunning \| attached` | yes | 起動結果。`attached` は既存 GUI Editor session の endpoint に接続した状態 |
| `daemonStatus` | `running` | yes | daemon 状態 |
| `lifecycleState` | `starting \| ready \| busy \| compiling \| domainReloading \| playmode \| blockedByModal \| safeMode \| shuttingDown` | yes | 起動または attach 直後の lifecycle snapshot。`daemon start` 成功は `ready` を保証しない |
| `blockingReason` | `null \| startup \| busy \| compile \| domainReload \| playMode \| modalDialog \| safeMode \| shutdown` | yes | 起動または attach 直後に通常実行を止めている理由 |
| `canAcceptExecutionRequests` | boolean | yes | 起動または attach 直後に通常実行要求を受け付けられるか。Play Mode 例外は含めない |
| `timeoutMilliseconds` | integer | yes | 実効タイムアウト |
| `session` | object | yes | セッション情報 |

`daemon start` の成功は endpoint と session の登録完了を意味し、`lifecycleState = ready` を保証しない。

失敗時の payload は session 未成立の起動観測結果を返す。`lifecycleState`、`blockingReason`、`canAcceptExecutionRequests` は endpoint 登録済み session の field であり、startup observation 失敗 payload には含めない。

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `startStatus` | `failed` | yes | 起動失敗 |
| `daemonStatus` | `notRunning \| stale` | yes | session 未成立後の daemon 状態 |
| `timeoutMilliseconds` | integer | yes | 実効タイムアウト |
| `session` | `null` | yes | endpoint と session が成立していないため常に `null` |
| `startup` | `object \| null` | yes | endpoint 登録前の起動観測結果。preflight 失敗など Unity process 起動前の失敗では `null` |
| `diagnosis` | `object \| null` | yes | startup failure の保存済みまたは推定 diagnosis |
| `retryDisposition` | `retryImmediately \| waitThenRetry \| retryAfterFix \| manualActionRequired \| doNotRetry \| unknown` | yes | failure payload から判断できる再試行方針 |
| `safeToRetryImmediately` | boolean | yes | 何も修正せず即時再試行してよい場合だけ `true` |

`daemonStatus=stale` は、既存 session artifact が存在するが endpoint probe 失敗、process 不在、token invalid などで再利用できない状態を表す。今回の launch attempt が endpoint 登録前に `blocked`、`timeout`、`failed` になっただけでは `stale` とせず、既存 stale session がなければ `notRunning` を返す。

`safeToRetryImmediately` は `retryDisposition=retryImmediately` のときだけ `true` とする。`waitThenRetry`、`retryAfterFix`、`manualActionRequired`、`doNotRetry`、`unknown` では `false` とする。

final `daemon start` failure payload では原則として `retryDisposition=waitThenRetry` を返さない。`waitThenRetry` は進行中 observation、将来の watch/diagnose 系 command、または timeout 前に観測途中状態を返す command のために予約する。

#### `daemon start payload.startup`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `startupStatus` | `launching \| waitingForEndpoint \| blocked \| timeout \| failed` | yes | endpoint 登録前の起動観測状態。failure payload では `completed` を返さない |
| `startupBlockingReason` | `null \| safeMode \| compile \| packageResolution \| ucliPlugin \| precompiledAssemblyConflict \| modalDialog \| endpointNotRegistered \| processExit \| unknown` | yes | startup を止めた理由。endpoint 登録後の `payload.blockingReason` とは別語彙 |
| `launchAttemptId` | `string \| null` | yes | 起動 attempt の識別子 |
| `editorMode` | `batchmode \| gui \| null` | yes | 起動または attach 対象の Editor mode |
| `ownerKind` | `cli \| user \| null` | yes | 起動 attempt の所有者種別 |
| `canShutdownProcess` | `boolean \| null` | yes | startup policy が process を終了対象にできるか |
| `processId` | `integer \| null` | yes | 関連 Unity process ID |
| `startedAtUtc` | `string \| null` | yes | 関連 Unity process の起動時刻 |
| `elapsedMilliseconds` | `integer \| null` | yes | 起動 attempt 開始から失敗判定までの経過時間 |
| `processAction` | `none \| kept \| terminated \| unknown` | yes | 起動失敗後に uCLI が process へ行った処理 |
| `processTermination` | `object \| null` | yes | process 終了を試みた場合の詳細。終了対象でない場合は `null` |
| `artifactPath` | `string \| null` | yes | 起動 attempt artifact directory または diagnosis artifact path |

#### `daemon start payload.startup.processTermination`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `attemptedGracefulShutdown` | boolean | yes | graceful shutdown を試みたか |
| `gracefulShutdownTimedOut` | boolean | yes | graceful shutdown が timeout したか |
| `forceKillAttempted` | boolean | yes | force kill を試みたか |
| `forceKillSucceeded` | `boolean \| null` | yes | force kill の成否。試みていない場合は `null` |
| `exitCode` | `integer \| null` | yes | 観測できた process exit code |
| `elapsedMilliseconds` | integer | yes | 終了処理に要した時間 |

#### `daemon stop`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `stopStatus` | `stopped \| notRunning` | yes | 停止結果。ユーザー起動 GUI Editor session では endpoint 登録解除までを `stopped` とする |
| `daemonStatus` | `notRunning` | yes | daemon 状態 |
| `timeoutMilliseconds` | integer | yes | 実効タイムアウト |
| `session` | `null` | yes | 停止後は常に `null` |

ユーザー起動 GUI Editor session の `daemon stop` は Unity process を終了せず、endpoint / session 登録と session token を無効化する。旧 session token では再接続できない。

#### `daemon cleanup`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `cleanupStatus` | `completed \| skipped` | yes | cleanup 結果 |
| `skipReason` | `null \| running \| unsafeInvalidSession \| uncertainReachability` | yes | cleanup を見送った理由 |
| `timeoutMilliseconds` | integer | yes | 実効タイムアウト |
| `deletedLaunchAttemptCount` | integer | yes | cleanup で削除した古い launch attempt artifact 件数。削除していない場合は `0` |

#### `daemon status`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `daemonStatus` | `running \| notRunning \| stale` | yes | daemon 状態 |
| `serverVersion` | `string \| null` | yes | `running` のときのみ値を持つ |
| `editorMode` | `batchmode \| gui \| null` | yes | `running` のときのみ値を持つ |
| `lifecycleState` | `starting \| ready \| busy \| compiling \| domainReloading \| playmode \| blockedByModal \| safeMode \| shuttingDown \| null` | yes | `running` のときのみ値を持つ。batchmode Editor session は `starting / ready / busy / compiling / domainReloading / playmode / shuttingDown` の subset を返し、GUI Editor session は `blockedByModal` と `safeMode` も返す |
| `blockingReason` | `null \| startup \| busy \| compile \| domainReload \| playMode \| modalDialog \| safeMode \| shutdown` | yes | `running` のときのみ意味を持つ。batchmode Editor session は `startup / busy / compile / domainReload / playMode / shutdown` の subset を返し、GUI Editor session は `modalDialog` と `safeMode` も返す |
| `compileState` | `ready \| compiling \| null` | yes | compiler activity 状態 |
| `compileGeneration` | `string \| null` | yes | compile 開始または完了ごとに変化する opaque な識別子 |
| `domainReloadGeneration` | `string \| null` | yes | `running` のときのみ値を持つ |
| `canAcceptExecutionRequests` | boolean | yes | 通常実行要求を受け付けられるときのみ `true`。`--allowPlayMode` の例外可否は含めず、`playmode` では `false` |
| `timeoutMilliseconds` | integer | yes | 実効タイムアウト |
| `session` | `object \| null` | yes | `running` / `stale` は object、`notRunning` は `null` |
| `diagnosis` | `object \| null` | yes | 保存済みまたは推定された diagnosis |
| `lastLaunchAttempt` | `object \| null` | yes | 直近の session 未成立 startup attempt。直近失敗がない場合、または running session の診断と重複する場合は `null` |

#### `daemon status payload.lastLaunchAttempt`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `launchAttemptId` | `string` | yes | 起動 attempt の識別子 |
| `startupStatus` | `blocked \| timeout \| failed` | yes | 直近の session 未成立 attempt の最終状態。running session と重複する `completed` attempt は返さない |
| `startupBlockingReason` | `null \| safeMode \| compile \| packageResolution \| ucliPlugin \| precompiledAssemblyConflict \| modalDialog \| endpointNotRegistered \| processExit \| unknown` | yes | startup を止めた理由 |
| `retryDisposition` | `retryImmediately \| waitThenRetry \| retryAfterFix \| manualActionRequired \| doNotRetry \| unknown` | yes | 直近 attempt の再試行方針 |
| `processAction` | `none \| kept \| terminated \| unknown` | yes | 起動失敗後に uCLI が process へ行った処理 |
| `artifactPath` | `string` | yes | `startup-diagnosis.json` の artifact path |
| `unityLogPath` | `string \| null` | yes | 参照先 Unity log path。log snapshot は複製しない |
| `updatedAtUtc` | string | yes | attempt 診断の更新時刻 |
| `processId` | `integer \| null` | yes | 起動 attempt に関連する process id |
| `processStartedAtUtc` | `string \| null` | yes | 起動 attempt に関連する process start timestamp |
| `diagnosis` | `object` | yes | attempt 単体で読める startup failure diagnosis |

#### `daemon list`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `timeoutMilliseconds` | integer | yes | コマンド全体に適用する実効タイムアウト |
| `projectRelativePath` | string | yes | Git worktree root から見た対象 Unity project の相対パス |
| `isComplete` | boolean | yes | 対象 worktree 群を最後まで観測できたか |
| `completionReason` | `null \| timeout` | yes | 不完全終了の理由 |
| `remainingWorktreeCount` | integer | yes | 未観測の worktree 件数 |
| `items` | array | yes | 永続化済み daemon 登録の一覧 |

#### `daemon session`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `projectFingerprint` | string | yes | project fingerprint |
| `issuedAtUtc` | string | yes | 発行時刻 |
| `editorMode` | `batchmode \| gui` | yes | Editor mode |
| `ownerKind` | `cli \| user` | yes | session 所有者種別。`user` はユーザーが起動済みの GUI Editor を表す |
| `canShutdownProcess` | boolean | yes | `daemon stop` が Unity process を終了してよいか。ユーザー起動 GUI Editor session では `false` |
| `endpointTransportKind` | string | yes | IPC transport 種別 |
| `endpointAddress` | string | yes | IPC endpoint |
| `processId` | `integer \| null` | yes | daemon process ID |

#### `daemon diagnosis`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `reason` | string | yes | 診断理由 |
| `message` | string | yes | 人間向け説明 |
| `reportedBy` | `unity \| cli` | yes | 診断報告元 |
| `isInferred` | boolean | yes | CLI が後から推定して合成した診断か |
| `confidence` | `high \| medium \| low \| unknown` | yes | 診断分類の信頼度 |
| `updatedAtUtc` | string | yes | 更新時刻 |
| `processId` | `integer \| null` | yes | 関連 process ID |
| `editorInstancePath` | `string \| null` | yes | 関連する `Library/EditorInstance.json` の path。該当なしは `null` |
| `processStartedAtUtc` | `string \| null` | yes | 関連 Unity process の起動時刻 |
| `unityLogPath` | `string \| null` | yes | 関連 Unity Editor log path |
| `startupPhase` | `string \| null` | yes | startup failure が観測された phase。例: `packageResolution`、`scriptCompilation`、`userAction`、`processExit` |
| `actionRequired` | `string \| null` | yes | 復旧に必要な行動。例: `fixCompileErrors`、`resolvePackages`、`resolveUnityDialog`、`inspectUnityLog` |
| `primaryDiagnostic` | `object \| null` | yes | 機械判定に使う代表診断 |
| `secondaryDiagnostics` | array | yes | 補助診断の配列。要素は `primaryDiagnostic` と同じ shape |
| `detectedSignals` | array | yes | 分類に使ったログまたは状態 signal |
| `topErrors` | array | yes | 復旧判断に使う上位 error 抜粋 |
| `artifactPath` | `string \| null` | yes | launch attempt artifact directory |
| `nextActions` | array | yes | 推奨される次操作。自動実行を意味しない |

#### `daemon diagnosis.primaryDiagnostic`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `kind` | string | yes | 診断種別。例: `compiler`、`packageResolution`、`unityDialog`、`processExit` |
| `category` | string | yes | 復帰カテゴリ。例: `compile`、`packageResolution`、`startup` |
| `code` | `string \| null` | yes | 種別内の詳細 code。NuGetForUnity 固有ログを検出できた場合の `NUGET_FOR_UNITY_RESTORE_FAILED` はここで表す |
| `source` | `string \| null` | yes | 診断の情報源。例: `Editor.log`、`upmLog`、`nugetForUnityLog`、`process` |
| `confidence` | `high \| medium \| low \| unknown` | yes | この診断単体の信頼度 |
| `file` | `string \| null` | yes | 関連 source file |
| `line` | `integer \| null` | yes | 関連行番号 |
| `column` | `integer \| null` | yes | 関連列番号 |
| `message` | string | yes | 代表診断メッセージ |

`errors[].code` は command failure の代表 code であり、`diagnosis.primaryDiagnostic.code` は診断根拠に近い詳細 code である。`primaryDiagnostic.code` は `errors[].code` と同じ open code set の値を使ってよいが、必ず `errors[]` にも載るとは限らない。agent は command outcome の分類には `errors[].code` と `retryDisposition` を使い、復帰カテゴリの詳細化には `diagnosis.reason` と `diagnosis.primaryDiagnostic` を使う。

#### `daemon diagnosis.detectedSignals[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `source` | string | yes | signal の情報源 |
| `patternId` | string | yes | 検出 pattern の識別子 |
| `lineExcerpt` | `string \| null` | yes | bounded なログ抜粋 |
| `confidence` | `high \| medium \| low \| unknown` | yes | signal の信頼度 |

#### `daemon diagnosis.topErrors[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `kind` | string | yes | error 種別 |
| `message` | string | yes | bounded な error message |
| `source` | string | yes | error の情報源 |
| `file` | `string \| null` | yes | 関連 source file |
| `line` | `integer \| null` | yes | 関連行番号 |

#### `daemon diagnosis.nextActions[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `kind` | string | yes | action 種別 |
| `summary` | string | yes | 人間向けの短い説明 |
| `command` | `string \| null` | yes | 実行候補コマンド。副作用が大きい修復操作は自動実行しない |

既存 GUI Editor process を検出したが endpoint 登録が timeout まで完了しない場合でも、起動ブロックを分類できる場合は `DAEMON_STARTUP_BLOCKED` の diagnosis を返す。分類不能のまま timeout した場合だけ、`IPC_TIMEOUT` の diagnosis は `reason = guiEndpointNotRegistered` または `endpointNotRegistered`、`reportedBy = cli`、`isInferred = true` とし、`editorInstancePath` と `processId` を返す。

#### `daemon list items[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `worktreePath` | string | yes | Git worktree root path |
| `branchRef` | `string \| null` | yes | attached 時の branch ref |
| `head` | string | yes | worktree の HEAD commit hash |
| `projectPath` | string | yes | Unity project root path |
| `projectFingerprint` | string | yes | worktree ローカル storage root で解決した fingerprint |
| `state` | `running \| stale \| error` | yes | item の状態 |
| `reason` | `null \| staleSession \| invalidSession \| probeTimeout \| probeFailed` | yes | item 状態の理由 |
| `issuedAtUtc` | `string \| null` | yes | valid session を読めた場合のみ値を持つ |
| `processId` | `integer \| null` | yes | valid session を読めた場合のみ値を持つ |
| `editorMode` | `batchmode \| gui \| null` | yes | valid session を読めた場合のみ値を持つ |
| `ownerKind` | `cli \| user \| null` | yes | valid session を読めた場合のみ値を持つ |
| `canShutdownProcess` | `boolean \| null` | yes | valid session を読めた場合のみ値を持つ |
| `endpointTransportKind` | `string \| null` | yes | valid session を読めた場合のみ値を持つ |
| `endpointAddress` | `string \| null` | yes | valid session を読めた場合のみ値を持つ |
| `diagnosis` | `object \| null` | yes | `running` は `null` |

### `ucli logs unity clear`
`ucli logs unity clear` は `command=logs.unity.clear` の `request-response` 型公開 CLI JSON 出力を返す。取得系の `ucli logs unity read` / `ucli logs daemon read` は stream 型であり、この payload 契約を使わない。

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `clearStatus` | `cleared` | yes | GUI Editor の Unity Console 表示のクリア結果 |
| `timeoutMilliseconds` | integer | yes | 実効タイムアウト |

このコマンドは GUI Editor の Unity Console 表示だけを対象とし、daemon log、Unity log stream、`.ucli` 配下の物理ログファイルは削除しない。

### `ucli init`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `configPath` | string | yes | 生成した `.ucli/config.json` の絶対パス |
| `gitignorePath` | string | yes | 生成した `.ucli/.gitignore` の絶対パス |

### `ucli refresh`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `project` | object | yes | 実行対象 project identity。shape は `payload.project` を参照 |
| `requestId` | string | yes | 内部 `execute` リクエストの `requestId` |
| `opResults` | array | yes | `ucli.project.refresh` の `call` 結果 |
| `readPostcondition` | object | no | mutation 後 read の safe 条件。shape は `IpcExecuteResponse.readPostcondition` を参照 |

### `ucli resolve`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `project` | object | yes | 実行対象 project identity。shape は `payload.project` を参照 |
| `requestId` | string | yes | 内部 `execute(command=resolve)` または index 解決単位の `requestId` |
| `opResults` | array | yes | `ucli.resolve` の `plan` 結果。成功時は単一要素 |
| `readIndex` | object | yes | 最終結果の観測元。shape は `payload.readIndex` を参照 |

### `ucli query`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `project` | object | yes | 実行対象 project identity。shape は `payload.project` を参照 |
| `requestId` | string | yes | 内部 `execute(command=query)` または index query 単位の `requestId` |
| `opResults` | array | yes | query 実行結果。成功時は単一要素 |
| `readIndex` | object | yes | 最終結果の観測元。shape は `payload.readIndex` を参照 |

### `ucli ops list`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `operations` | array | yes | 利用可能な operation の一覧。絞り込み条件に該当しない場合は空配列 |
| `readIndex` | object | yes | catalog / detail の観測元。shape は `payload.readIndex` を参照 |

#### `ucli ops list payload.operations[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `name` | string | yes | operation 名 |
| `kind` | string | yes | validation 済みの operation kind。`query`、`command`、または `mutation` |
| `policy` | string | yes | contract facts から導出された admission policy。`safe`、`advanced`、または `dangerous` |
| `description` | string | yes | operation の目的、使いどころ、注意点 |

`ucli ops list` は public raw `kind:"op"` として呼べる operation だけを返す。`editLoweringOnly` と `internal` の operation は public `ops list` に出さない。一覧は絞り込みに必要な `name` / `kind` / `policy` と、operation 選択に必要な短い `description` だけを返す。`inputs`、`resultContract`、`assurance`、`argsSchema`、`resultSchema` は含めない。operation の詳細契約は `ucli ops describe <opName>` を参照する。

### `ucli ops describe`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `operation` | object | yes | 対象 operation の詳細 |
| `readIndex` | object | yes | catalog / detail の観測元。shape は `payload.readIndex` を参照 |

`ucli ops describe` は指定した単一 public raw operation の detail を返す。`editLoweringOnly` と `internal` の operation は public `ops describe` の対象外であり、public CLI から指定された場合は見つからない operation として扱う。readIndex の永続化形式が list descriptor と describe detail に分かれていても、公開 payload の shape は変えない。

#### `ucli ops describe payload.operation`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `name` | string | yes | operation 名 |
| `kind` | string | yes | validation 済みの operation kind。`query`、`command`、または `mutation`。`command` は Editor 状態や AssetDatabase 状態を変えるが、永続化対象の内容変更を主目的にしない |
| `policy` | string | yes | contract facts から導出された admission policy。`safe`、`advanced`、または `dangerous` |
| `description` | string | yes | operation の目的、使いどころ、注意点 |
| `inputs` | array | yes | ユーザー入力から `steps[].args` を組み立てるための主契約。shape は `payload.operation.inputs[]` を参照 |
| `resultContract` | object | yes | `opResults[].result` の有無、Result contract 型名、読み方。shape は `payload.operation.resultContract` を参照 |
| `codeContract` | object | no | source code を受け取る operation の entry point 署名、uCLI が提供する source-visible API、戻り値制約。shape は `payload.operation.codeContract` を参照 |
| `assurance` | object | yes | 副作用と plan / touched の保証情報。shape は `payload.operation.assurance` を参照 |
| `argsSchema` | object | yes | `steps[].args` の JSON 構造検証用 JSON Schema |
| `resultSchema` | object \| null | yes | `opResults[].result` の JSON 構造検証用 JSON Schema。`UcliNoResult` operation では `null` |

`policy` はこの operation の唯一の公開 admission policy である。policy の別表現、導出履歴、reason list、author 指定値は public payload に含めない。導出規則は catalog validation と contract test の対象であり、agent / runner は `policy` と `assurance` の contract facts を使って admission を判断する。

`argsSchema` / `resultSchema` は検証用 schema であり、agent 向けの主契約ではない。operation 選択、`args` の組み立て、admission 判断、結果解釈は `description` / `inputs[].constraints` / `inputs[].variants[].fields[].constraints` / `resultContract` / `assurance` を参照する。source code を受け取る operation の C# API は `codeContract` を参照する。schema には説明文や意味制約を置かない。

#### `ucli ops describe payload.operation.inputs[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `name` | string | yes | input 名。通常は対応する args property 名 |
| `description` | string | yes | 入力値の意味 |
| `valueType` | string | yes | JSON 値型。`string`、`boolean`、`integer`、`number`、`object`、`array` のいずれか |
| `constraints` | array | yes | 入力値の意味制約。shape は `payload.operation.inputs[].constraints[]` を参照 |
| `argsPath` | string | no | 対応する `steps[].args` 内 JSON path。省略時は `$.<name>` |
| `variants` | array | no | selector / reference の表現方法。shape は `payload.operation.inputs[].variants[]` を参照 |

`valueType` は IPC JSON の値型だけを表す。C# contract では `SceneAssetPath`、`PrefabAssetPath`、`UnityHierarchyPath`、`UnityGlobalObjectId`、`UnityAssetGuid`、`UnityTypeId` などの semantic value type を使えるが、`ops describe` ではそれらも JSON wire shape に合わせて `valueType:"string"` として出す。意味の違いは型名をそのまま外へ出すのではなく、`description` と `constraints` に展開する。

必須性は `argsSchema.required` で表す。optional input は `argsSchema.required` に含めない。`inputs[]` には `required` field を持たせない。

`argsPath` は例外用である。input 名と args property 名が一致する通常ケースでは省略する。root object 全体を 1 input として扱う場合や、input 名と JSON property 名が一致しない場合だけ指定する。

`constraints` は常に出す。意味制約がない input は `constraints: []` とする。

`argsPath` と `variants[].fields[].argsPath` は JSONPath ではなく uCLI args path である。`inputs[].argsPath` の許可形は `$`、`$.property`、`$.property.nestedProperty` だけとする。`variants[].fields[].argsPath` は具体的な field path なので、許可形は `$.property`、`$.property.nestedProperty` だけとする。各 property segment は ASCII 英数字と `_` だけを使い、全体は 256 文字以内、最大 16 segment とする。配列添字、wildcard、filter、quoted property name は扱わない。`var` segment は request-local alias 用の内部 branch なので、public `ops describe` には出さない。`variants[].fields[].argsPath` は同じ input の `argsPath`、または省略時の `$.<name>` と同じ path か、その descendant path でなければならない。

`argsPath` を指定する例:

```json
{
  "name": "patch",
  "valueType": "object",
  "description": "Serialized property values to apply.",
  "constraints": [
    { "kind": "serializedProperty", "access": "write" }
  ],
  "argsPath": "$.values"
}
```

この例では agent-facing input 名は `patch` だが、実際の `steps[].args` では `values` property を組み立てる。

#### `ucli ops describe payload.operation.inputs[].variants[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `name` | string | yes | variant 名 |
| `description` | string | yes | この表現方法の意味 |
| `fields` | array | yes | この variant を成立させるために埋める field 群。shape は `payload.operation.inputs[].variants[].fields[]` を参照 |

variant は operation の意味差ではなく、同じ input を表現する方法だけを表す。同一 `input` 内の `variants` は相互排他である。operation の `description` が複数のユーザー意図を説明する場合、その operation は分割対象である。

variant を選ぶ場合、agent はその `variants[].fields[]` に列挙された field をすべて埋める。variant 固有の optional field は `fields[]` には含めず、`argsSchema` の optional property と operation / input の `description` で表す。

path と constraint は field object に閉じ込める。variant 直下に constraint を置かない。

#### `ucli ops describe payload.operation.inputs[].variants[].fields[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `name` | string | yes | variant 内 field 名 |
| `argsPath` | string | yes | この field が対応する `steps[].args` 内 path |
| `description` | string | yes | field 値の意味 |
| `constraints` | array | yes | field 値の意味制約。shape は `payload.operation.inputs[].constraints[]` と同じ |

field の `constraints` は常に出す。意味制約がない field は `constraints: []` とする。

field の `name` は `argsPath` の最後の property segment と一致しなければならない。たとえば `argsPath:"$.target.globalObjectId"` の field 名は `globalObjectId` である。

#### `ucli ops describe payload.operation.inputs[].constraints[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `kind` | string | yes | 制約の種類 |
| `min` | number | no | `kind:"range"` の下限 |
| `max` | number | no | `kind:"range"` の上限 |
| `assetKind` | string | no | `asset`、`scene`、`prefab`、または `projectSettings` |
| `targetKind` | string | no | `asset`、`gameObject`、または `component` |
| `typeKind` | string | no | `component` |
| `access` | string | no | `write` |

`constraints` は入力値の意味を表す機械判定用 contract である。自由文ではなく、定義済み `kind` と必要なパラメータで表す。

初期 vocabulary は次に限定する。

| kind | Parameters | Meaning |
| --- | --- | --- |
| `nonEmpty` | none | 空文字、空配列、空 object を受け付けない |
| `range` | `min?`, `max?` | 数値が指定範囲に入る。下限だけ、上限だけ、両方を同じ kind で表す |
| `projectRelativePath` | none | Unity project 相対 path として扱う |
| `assetExists` | `assetKind` | 指定 kind の既存 asset を指す |
| `assetCreatable` | `assetKind` | 指定 kind の asset 作成先として使える |
| `globalObjectId` | none | Unity GlobalObjectId として解決できる |
| `assetGuid` | none | Unity asset GUID として解釈する |
| `hierarchyPath` | none | scene / prefab hierarchy path として解釈する |
| `referenceResolvable` | `targetKind` | 指定 kind の Unity 参照へ解決できる |
| `typeExists` | none | Unity runtime type として解決できる |
| `typeAssignableTo` | `typeKind` | 指定 kind に代入可能な Unity type である |
| `serializedProperty` | `access` | serialized property path として書き込みに使える |

kind ごとの parameter 規則:

| kind | Required parameters | Forbidden parameters |
| --- | --- | --- |
| `nonEmpty` | none | `min`, `max`, `assetKind`, `targetKind`, `typeKind`, `access` |
| `range` | `min` または `max` の少なくとも一方 | `assetKind`, `targetKind`, `typeKind`, `access` |
| `projectRelativePath` | none | `min`, `max`, `assetKind`, `targetKind`, `typeKind`, `access` |
| `assetExists` | `assetKind` | `min`, `max`, `targetKind`, `typeKind`, `access` |
| `assetCreatable` | `assetKind` | `min`, `max`, `targetKind`, `typeKind`, `access` |
| `globalObjectId` | none | `min`, `max`, `assetKind`, `targetKind`, `typeKind`, `access` |
| `assetGuid` | none | `min`, `max`, `assetKind`, `targetKind`, `typeKind`, `access` |
| `hierarchyPath` | none | `min`, `max`, `assetKind`, `targetKind`, `typeKind`, `access` |
| `referenceResolvable` | `targetKind` | `min`, `max`, `assetKind`, `typeKind`, `access` |
| `typeExists` | none | `min`, `max`, `assetKind`, `targetKind`, `typeKind`, `access` |
| `typeAssignableTo` | `typeKind` | `min`, `max`, `assetKind`, `targetKind`, `access` |
| `serializedProperty` | `access` | `min`, `max`, `assetKind`, `targetKind`, `typeKind` |

#### `ucli ops describe payload.operation.resultContract`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `emitted` | boolean | yes | `opResults[].result` が出るか |
| `resultType` | string | yes | result の contract 型名。result を返さない operation は `UcliNoResult` |
| `description` | string | yes | result の意味と読み方 |

`resultContract` は `opResults[].result` の読み方だけを表す。step 間データフロー、binding、durable output はこの property では表さない。

`emitted:false` の operation は `opResults[].result` property 自体を出さない。`null` は返さない。この場合、`resultType` は `UcliNoResult`、`resultSchema` は `null` になる。

`UcliNoResult` は `{}` または `null` を意味しない。operation 固有の result field を出力しないことを意味する。

#### `ucli ops describe payload.operation.codeContract`

`codeContract` は source code を受け取る operation だけが返す任意 property である。JSON wire の `args` / `result` 構造ではなく、利用者が渡す source code の entry point と uCLI が提供する API を説明する。Unity API や project assembly の参照可否を列挙する allowlist ではない。

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `language` | string | yes | source language。`ucli.cs.eval` は `csharp` |
| `entryPoint` | object | yes | 利用者 source に必要な entry point。shape は `payload.operation.codeContract.entryPoint` を参照 |
| `sourceForms` | array | yes | 受け付ける source form。shape は `payload.operation.codeContract.sourceForms[]` を参照 |
| `apiTypes` | array | yes | uCLI が利用者 source 向けに公開した API 型。shape は `payload.operation.codeContract.apiTypes[]` を参照 |

`apiTypes` の説明は対象 API の `[UcliDescription]` から生成する。説明を持たない public member または parameter がある API 型は `codeContract` 生成に失敗する。

#### `ucli ops describe payload.operation.codeContract.entryPoint`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `signature` | string | yes | source に実装する entry point の C# 署名 |
| `matchRule` | string | yes | source 内から entry point を選択する規則 |
| `requiredStatic` | boolean | yes | entry point が `static` でなければならないか |
| `parameterTypes` | string[] | yes | entry point parameter の完全修飾型名 |
| `returnValue` | string | yes | 戻り値制約 |

#### `ucli ops describe payload.operation.codeContract.sourceForms[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `kind` | string | yes | source form 名。`ucli.cs.eval` は `compilationUnit` または `snippet` |
| `description` | string | yes | source form の書き方と制約 |

#### `ucli ops describe payload.operation.codeContract.apiTypes[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `name` | string | yes | API 型名 |
| `fullName` | string | yes | API 型の完全修飾名 |
| `description` | string | yes | API 型の説明 |
| `members` | array | yes | public API member。shape は `payload.operation.codeContract.apiTypes[].members[]` を参照 |

#### `ucli ops describe payload.operation.codeContract.apiTypes[].members[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `kind` | `method \| property` | yes | member 種別 |
| `name` | string | yes | member 名 |
| `description` | string | yes | member の説明 |
| `type` | string | property のみ | property 型 |
| `returnType` | string | method のみ | method 戻り値型 |
| `parameters` | array | method のみ | method parameter。shape は `payload.operation.codeContract.apiTypes[].members[].parameters[]` を参照 |

#### `ucli ops describe payload.operation.codeContract.apiTypes[].members[].parameters[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `name` | string | yes | parameter 名 |
| `type` | string | yes | parameter 型 |
| `description` | string | yes | parameter の説明 |

#### `ucli ops describe payload.operation.assurance`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `sideEffects` | string[] | yes | `call` で起こり得る副作用の分類 |
| `mayDirty` | boolean | yes | `call` が Unity object や project state を dirty にし得るか |
| `mayPersist` | boolean | yes | `call` が project file へ永続化し得るか |
| `touchedKinds` | string[] | yes | dirty / persist / post-read safety の責任対象として `touched[]` に出現し得る resource kind |
| `planMode` | string | yes | public v1 catalog では `validationOnly` または `observesLiveUnity` |
| `planSemantics` | string | yes | `plan` が検証・観測する内容と、実行しない内容 |
| `callSemantics` | string | yes | `call` が適用する内容と、適用境界 |
| `touchedContract` | string | yes | `touched[]` を返す条件と、返さない条件 |
| `readPostconditionContract` | string | yes | mutation 後に stale になり得る read surface と safe 条件 |
| `failureSemantics` | string | yes | timeout、cancel、partial apply、indeterminate の扱い |
| `dangerousNotes` | string[] | yes | dangerous / advanced operation の保証外領域。該当なしは空配列 |

`sideEffects` は閉じた語彙で表す。副作用がない operation は `sideEffects: []` とする。

| sideEffects value | Meaning |
| --- | --- |
| `observesUnityState` | live Unity state または readIndex を観測する |
| `editorStateChange` | Editor の開いている Scene、Prefab Stage、selection などを変え得る |
| `opensSceneInEditor` | Unity Editor で Scene を開く |
| `opensPrefabStage` | Prefab editing stage を開く |
| `assetDatabaseRefresh` | AssetDatabase refresh を実行する |
| `assetImport` | AssetDatabase import を発生させ得る |
| `scriptCompilation` | script compilation を発生させ得る |
| `domainReload` | domain reload を発生させ得る |
| `sceneContentMutation` | Scene content を dirty にし得る |
| `prefabContentMutation` | Prefab asset content を dirty にし得る |
| `assetContentMutation` | AssetDatabase 管理下の asset content を dirty にし得る |
| `projectSettingsMutation` | ProjectSettings 配下の project setting を dirty にし得る |
| `sceneSave` | Scene file へ永続化し得る |
| `prefabSave` | Prefab asset file へ永続化し得る |
| `assetSave` | project asset file へ永続化し得る |
| `projectSave` | project-wide save を実行し得る |
| `externalProcess` | 外部 process または shell を実行し得る |
| `filesystemWrite` | Unity Editor API の save boundary 外で filesystem write を行い得る |
| `arbitrarySourceExecution` | 利用者が渡した source code を実行し得る |
| `destructiveScope` | delete / overwrite など破壊的操作を行い得る |

`touchedKinds` は閉じた語彙で表す。観測だけを行う query operation は読み取り対象を `inputs[]`、semantic constraints、`description` で表し、`touchedKinds: []` とする。touched resource がない operation も `touchedKinds: []` とする。

| touchedKinds value | Meaning |
| --- | --- |
| `scene` | Scene resource |
| `prefab` | Prefab resource |
| `asset` | AssetDatabase 管理下の project asset |
| `projectSettings` | ProjectSettings 配下の project setting |

public v1 catalog の `planMode` は次のいずれかに限定する。

| planMode value | Meaning |
| --- | --- |
| `validationOnly` | Plan は typed args と静的 contract の検証だけを行う |
| `observesLiveUnity` | Plan は live Unity state または readIndex を観測して結果を作る |

`mayCreatePreviewState` は public v1 catalog に含めない予約値である。internal / experimental catalog で扱う場合でも、最低 `advanced`、cleanup evidence と residual risk が不十分な場合は `dangerous` とする。

`kind` と `assurance` の整合は次を満たす。

| kind | Assurance rule | Examples |
| --- | --- | --- |
| `query` | 観測のみ。`mayDirty:false`、`mayPersist:false`、`touchedKinds:[]` とする。実行結果は `applied=false`、`changed=false`、`touched=[]` でなければならない。読み取り対象は `inputs[]`、semantic constraints、`description` で表し、Editor 状態や永続化単位への変更を残してはならない | `ucli.scene.tree`, `ucli.assets.find` |
| `command` | Editor 状態や AssetDatabase 状態を変え得る。永続化対象の内容変更を主目的にしないが、import や AssetDatabase refresh によって dirty / persist が起こり得る場合は `mayDirty` / `mayPersist` を `true` にし、副作用は `sideEffects` で表す | `ucli.scene.open`, `ucli.prefab.open`, `ucli.project.refresh` |
| `mutation` | Scene / Prefab / Asset / ProjectSettings を dirty または保存し得る。対象種別は `touchedKinds`、永続化可能性は `mayPersist` で表す | `ucli.comp.set`, `ucli.scene.save` |

policy 導出では、write は一律 `dangerous` ではない。Unity Editor API 経由で context-bound に実行でき、typed args、touched boundary、save boundary を保証できる deterministic write は `advanced` とする。任意 source 実行、任意 shell / process / filesystem write、unbounded destructive operation、または touched / save boundary を十分に保証できない escape hatch は `dangerous` とする。

実行結果は assurance facts と整合しなければならない。`changed=true` なのに `mayDirty=false`、永続化効果があるのに `mayPersist=false`、`touched[].kind` が `assurance.touchedKinds` に含まれない、`kind=query` なのに `applied=true`、`changed=true`、または `touched[]` が non-empty になる、という結果は `OPERATION_CONTRACT_VIOLATION` として扱う。

#### `ucli ops describe payload.operation.argsSchema`

`argsSchema` は `steps[].args` の JSON 構造だけを表す。使用する語彙は `type`、`properties`、`required`、`additionalProperties:false`、`items`、`$ref`、`$defs` に限定する。`type` は string または string array で、値は `object`、`array`、`string`、`integer`、`number`、`boolean`、`null` のいずれかである。

schema には説明文や意味制約を出さない。説明は `description`、input 全体の意味制約は `inputs[].constraints`、variant field の意味制約は `inputs[].variants[].fields[].constraints` に置く。

#### `ucli ops describe payload.operation.resultSchema`

`argsSchema` / `resultSchema` は JSON Schema の完全実装ではなく、uCLI-supported JSON Schema subset である。この subset は JSON object の構造検証だけを contract し、外部 JSON Schema validator への完全互換入力として扱えることは保証しない。

subset で使用できる語彙は `type`、`properties`、`required`、`additionalProperties:false`、`items`、`$ref`、`$defs` に限定する。`type` は string または string array で、nullable property は `["<type>","null"]` で表す。`$schema` は出力しない。closed value set は schema の `enum` ではなく、この property reference の語彙表、`inputs[].constraints`、または `inputs[].variants[].fields[].constraints` で表す。composition、condition、default、example、format、scalar constraint 系の JSON Schema keyword は公開 contract として使用しない。

`resultSchema` は `opResults[].result` の JSON 構造だけを表す。result を返さない operation では `null` になる。配列 property は `items` で要素構造を表し、ネスト型や再帰型は `$defs` の名前付き schema と `$ref` で表す。

`$defs` は配列ではなく object である。複数定義は `"SceneTreeNode"` や `"AssetReference"` のような名前を key にして並べ、参照側は `"$ref":"#/$defs/SceneTreeNode"` の形で参照する。

#### `ucli ops describe` no result command 例

```json
{
  "operation": {
    "name": "ucli.scene.open",
    "kind": "command",
    "policy": "advanced",
    "description": "Open a Unity scene in the Editor before reading or editing scene objects.",
    "inputs": [
      {
        "name": "path",
        "valueType": "string",
        "description": "Project-relative path to a scene asset.",
        "constraints": [
          { "kind": "nonEmpty" },
          { "kind": "assetExists", "assetKind": "scene" }
        ]
      }
    ],
    "resultContract": {
      "emitted": false,
      "resultType": "UcliNoResult",
      "description": "No operation-specific result is emitted."
    },
    "assurance": {
      "sideEffects": [
        "opensSceneInEditor"
      ],
      "mayDirty": false,
      "mayPersist": false,
      "touchedKinds": [],
      "planMode": "observesLiveUnity",
      "planSemantics": "Validate the scene path and observe whether the scene can be opened.",
      "callSemantics": "Open the requested scene in the Unity Editor without saving project data.",
      "touchedContract": "Does not report touched resources because no dirty or persisted resource is produced.",
      "readPostconditionContract": "Does not stale read surfaces by itself.",
      "failureSemantics": "Timeout or domain reload may leave the open state indeterminate.",
      "dangerousNotes": []
    },
    "argsSchema": {
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "path": {
          "type": "string"
        }
      },
      "required": [
        "path"
      ]
    },
    "resultSchema": null
  },
  "readIndex": {
    "used": true,
    "hit": true,
    "source": "index",
    "freshness": "fresh",
    "generatedAtUtc": "2026-05-03T00:00:00Z",
    "fallbackReason": null
  }
}
```

#### object result と nested `$defs` 例

```json
{
  "operation": {
    "name": "ucli.scene.tree",
    "kind": "query",
    "policy": "safe",
    "description": "Read the hierarchy tree for a loaded or indexed Unity scene.",
    "inputs": [
      {
        "name": "path",
        "valueType": "string",
        "description": "Scene asset path to inspect.",
        "constraints": [
          { "kind": "nonEmpty" },
          { "kind": "projectRelativePath" },
          { "kind": "assetExists", "assetKind": "scene" }
        ]
      },
      {
        "name": "depth",
        "valueType": "integer",
        "description": "Maximum hierarchy depth to include; null means unbounded.",
        "constraints": [
          { "kind": "range", "min": 0 }
        ]
      },
      {
        "name": "limit",
        "valueType": "integer",
        "description": "Maximum number of hierarchy nodes to include in the response window.",
        "constraints": [
          { "kind": "range", "min": 1, "max": 10000 }
        ]
      },
      {
        "name": "cursor",
        "valueType": "string",
        "description": "Opaque cursor returned by the previous scene tree window.",
        "constraints": [
          { "kind": "cursor" }
        ]
      }
    ],
    "resultContract": {
      "emitted": true,
      "resultType": "SceneTreeResult",
      "description": "Scene hierarchy tree for the requested scene."
    },
    "assurance": {
      "sideEffects": [],
      "mayDirty": false,
      "mayPersist": false,
      "touchedKinds": [],
      "planMode": "observesLiveUnity",
      "planSemantics": "Validate the scene path and observe the selected hierarchy source.",
      "callSemantics": "Read the scene hierarchy without applying mutation.",
      "touchedContract": "Does not report touched resources because no dirty or persisted resource is produced.",
      "readPostconditionContract": "Does not stale read surfaces by itself.",
      "failureSemantics": "Timeout or source fallback failure means the hierarchy was not fully observed.",
      "dangerousNotes": []
    },
    "argsSchema": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "path"
      ],
      "properties": {
        "path": {
          "type": "string"
        },
        "depth": {
          "type": [
            "integer",
            "null"
          ]
        },
        "limit": {
          "type": [
            "integer",
            "null"
          ]
        },
        "cursor": {
          "type": [
            "string",
            "null"
          ]
        }
      }
    },
    "resultSchema": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "path",
        "roots",
        "sourceState",
        "window"
      ],
      "properties": {
        "path": {
          "type": "string"
        },
        "roots": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/IndexSceneTreeLiteNodeJsonContract"
          }
        },
        "sourceState": {
          "$ref": "#/$defs/SceneTreeSourceState"
        },
        "window": {
          "$ref": "#/$defs/BoundedWindow"
        }
      },
      "$defs": {
        "IndexSceneTreeLiteNodeJsonContract": {
          "type": "object",
          "additionalProperties": false,
          "required": [
            "name",
            "globalObjectId",
            "children",
            "childrenState"
          ],
          "properties": {
            "name": {
              "type": "string"
            },
            "globalObjectId": {
              "type": "string"
            },
            "children": {
              "type": "array",
              "items": {
                "$ref": "#/$defs/IndexSceneTreeLiteNodeJsonContract"
              }
            },
            "childrenState": {
              "type": "string",
              "enum": [
                "complete",
                "notExpandedByDepth",
                "truncatedByWindow",
                "unknown"
              ]
            }
          }
        },
        "SceneTreeSourceState": {
          "type": "object",
          "additionalProperties": false,
          "required": [
            "kind",
            "isDirty"
          ],
          "properties": {
            "kind": {
              "type": "string"
            },
            "isDirty": {
              "type": "boolean"
            }
          }
        },
        "BoundedWindow": {
          "type": "object",
          "additionalProperties": false,
          "required": [
            "limit",
            "cursor",
            "nextCursor",
            "isComplete",
            "totalCount"
          ],
          "properties": {
            "limit": {
              "type": "integer"
            },
            "cursor": {
              "type": [
                "string",
                "null"
              ]
            },
            "nextCursor": {
              "type": [
                "string",
                "null"
              ]
            },
            "isComplete": {
              "type": "boolean"
            },
            "totalCount": {
              "type": [
                "integer",
                "null"
              ]
            }
          }
        }
      }
    }
  },
  "readIndex": {
    "used": true,
    "hit": true,
    "source": "index",
    "freshness": "fresh",
    "generatedAtUtc": "2026-05-03T00:00:00Z",
    "fallbackReason": null
  }
}
```

`depth` は optional input なので `argsSchema.required` に含めない。`inputs[]` に `required` は置かない。

#### object input と selector variants 例

```json
{
  "input": {
    "name": "target",
    "valueType": "object",
    "description": "Object reference to resolve.",
    "argsPath": "$.target",
    "constraints": [
      { "kind": "referenceResolvable", "targetKind": "gameObject" }
    ],
    "variants": [
      {
        "name": "globalObjectId",
        "description": "Use when an exact Unity GlobalObjectId is already known.",
        "fields": [
          {
            "name": "globalObjectId",
            "argsPath": "$.target.globalObjectId",
            "description": "Resolved Unity GlobalObjectId.",
            "constraints": [
              { "kind": "globalObjectId" }
            ]
          }
        ]
      },
      {
        "name": "sceneHierarchy",
        "description": "Use when the scene path and hierarchy path are known.",
        "fields": [
          {
            "name": "scene",
            "argsPath": "$.target.scene",
            "description": "Scene asset path for a hierarchy selector.",
            "constraints": [
              { "kind": "assetExists", "assetKind": "scene" }
            ]
          },
          {
            "name": "hierarchyPath",
            "argsPath": "$.target.hierarchyPath",
            "description": "Unity hierarchy path inside the selected scene or prefab.",
            "constraints": [
              { "kind": "hierarchyPath" }
            ]
          }
        ]
      }
    ]
  },
  "argsSchema": {
    "type": "object",
    "additionalProperties": false,
    "required": [
      "target"
    ],
    "properties": {
      "target": {
        "type": "object",
        "additionalProperties": false,
        "properties": {
          "globalObjectId": {
            "type": "string"
          },
          "scene": {
            "type": "string"
          },
          "hierarchyPath": {
            "type": "string"
          }
        }
      }
    }
  }
}
```

object input の内部構造は `argsSchema.properties` で表す。selector の表現差は `variants` に置くが、operation の意味差は variant にしない。

selector の各 variant は同じ `target` object の異なる表現方法を説明する。`variants[].fields[]` はその variant を選ぶときに埋める leaf property と、各 property に掛かる説明・意味制約を表す。`argsSchema.properties.target.properties` はそれらの leaf property の JSON 構造だけを表す。

複数表現の排他性は `inputs[].variants[]` の仕様で表し、JSON Schema の `oneOf` では表さない。

#### root object input の `argsPath` 例

```json
{
  "input": {
    "name": "request",
    "valueType": "object",
    "description": "Complete operation argument object.",
    "constraints": [],
    "argsPath": "$"
  },
  "argsSchema": {
    "type": "object",
    "additionalProperties": false,
    "required": [
      "path",
      "values"
    ],
    "properties": {
      "path": {
        "type": "string"
      },
      "values": {
        "type": "object"
      }
    }
  }
}
```

`argsPath:"$"` は input が `steps[].args` 全体に対応することを表す。通常の property input では `argsPath` を省略する。

#### no constraints 例

```json
{
  "name": "includeInactive",
  "valueType": "boolean",
  "description": "Whether inactive objects are included.",
  "constraints": []
}
```

意味制約がない input でも `constraints` は省略しない。

#### 複数 `$defs` 例

```json
{
  "resultSchema": {
    "type": "object",
    "additionalProperties": false,
    "required": [
      "matches"
    ],
    "properties": {
      "matches": {
        "type": "array",
        "items": {
          "$ref": "#/$defs/AssetMatch"
        }
      },
      "selected": {
        "$ref": "#/$defs/AssetReference"
      }
    },
    "$defs": {
      "AssetMatch": {
        "type": "object",
        "additionalProperties": false,
        "required": [
          "assetPath",
          "assetGuid"
        ],
        "properties": {
          "assetPath": {
            "type": "string"
          },
          "assetGuid": {
            "type": "string"
          }
        }
      },
      "AssetReference": {
        "type": "object",
        "additionalProperties": false,
        "required": [
          "assetGuid"
        ],
        "properties": {
          "assetGuid": {
            "type": "string"
          }
        }
      }
    }
  }
}
```

`$defs` は名前付き schema の object であり、複数定義を持つ場合も配列にはしない。

#### 禁止される表現

`description` が複数のユーザー意図を説明する operation は分割対象である。`variants` は同じ input の参照方法だけに使う。

schema の property に説明文や意味制約を置かない。説明は `inputs[].description` または `inputs[].variants[].fields[].description`、意味制約は `inputs[].constraints` または `inputs[].variants[].fields[].constraints` に置く。空文字禁止は該当 input または field の `constraints` の `{ "kind": "nonEmpty" }` で表す。

#### 不正な result 例

```json
{
  "resultContract": {
    "emitted": false,
    "resultType": "UcliNoResult",
    "description": "No operation-specific result is emitted."
  },
  "resultSchema": null,
  "opResults": [
    {
      "id": "openMainScene",
      "phase": "call",
      "applied": true,
      "changed": false,
      "result": null
    }
  ]
}
```

`emitted:false` では `opResults[].result` property 自体を出さない。`null` は返さない。

### `ucli validate`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `project` | object | yes | 検証対象 project identity。shape は `payload.project` を参照 |
| `readIndex` | object | yes | この静的検証が参照した readIndex 情報。shape は `payload.readIndex` を参照 |

`validate` は Play Mode 変更の runtime 条件、対象 live object、Prefab instance lineage、request-attributed property path を保証しない。これらは `plan --allowPlayMode` で検証する。

### `ucli plan`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `project` | object | yes | 実行対象 project identity。shape は `payload.project` を参照 |
| `requestId` | string | yes | 内部 `execute(command=plan)` リクエストの `requestId` |
| `opResults` | array | yes | `plan` 実行結果。failure 時は `[]` または部分結果 |
| `readIndex` | object | yes | Unity IPC `plan` 実行前の static preflight で再利用した readIndex 情報。shape は `payload.readIndex` を参照 |
| `planToken` | string | no | `plan` 成功時に発行されたトークン。failure 時は field 自体を省略する |

### `ucli call`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `project` | object | yes | 実行対象 project identity。shape は `payload.project` を参照 |
| `requestId` | string | yes | 内部 `execute(command=call)` リクエストの `requestId` |
| `opResults` | array | yes | `call` 実行結果。failure 時は `[]` または部分結果 |
| `readPostcondition` | object | no | mutation 後 read の safe 条件。shape は `IpcExecuteResponse.readPostcondition` を参照 |
| `plan` | object | no | `--withPlan` 指定時の事前 `plan` 結果 |

#### `ucli call payload.plan`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `project` | object | yes | 事前 plan の実行対象 project identity。top-level `payload.project` と同じ identity |
| `requestId` | string | yes | 事前 `execute(command=plan)` リクエストの `requestId` |
| `opResults` | array | yes | `plan` 実行結果。failure 時は `[]` または部分結果 |
| `planToken` | string | no | 事前 `plan` 成功時に発行されたトークン。未発行時は field 自体を省略する |

`payload.plan` は `readPostcondition` を含まない。mutation 後 read の safe 条件は常に top-level の `payload.readPostcondition` にだけ出力する。

### readIndex

#### `payload.readIndex`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `used` | boolean | yes | readIndex を利用したか |
| `hit` | boolean | yes | 既存 index を利用できたか |
| `source` | string | yes | 参照元 |
| `freshness` | `fresh \| probable \| stale` | yes | 索引の鮮度 |
| `generatedAtUtc` | `string \| null` | yes | index 生成時刻 |
| `fallbackReason` | `string \| null` | yes | フォールバック理由 |

#### `types.catalog.json`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `schemaVersion` | integer | yes | catalog schema version |
| `generatedAtUtc` | string | yes | 生成時刻 |
| `sourceInputsHash` | string | yes | 入力ハッシュ |
| `entries` | array | yes | type エントリ一覧 |

#### `types.catalog.json entries[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `typeId` | string | yes | 型識別子 |
| `displayName` | string | yes | 表示名 |
| `namespace` | `string \| null` | yes | 名前空間 |
| `assemblyName` | string | yes | アセンブリ名 |
| `baseTypeId` | `string \| null` | yes | 基底型識別子 |
| `flags.isAbstract` | boolean | yes | abstract 型か |
| `flags.isGenericDefinition` | boolean | yes | generic definition か |
| `flags.isUnityObject` | boolean | yes | UnityEngine.Object 派生か |
| `flags.isComponent` | boolean | yes | Component か |
| `flags.isScriptableObject` | boolean | yes | ScriptableObject か |
| `flags.isSerializeReferenceCandidate` | boolean | yes | SerializeReference 候補か |

#### `schemas.catalog.json`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `schemaVersion` | integer | yes | catalog schema version |
| `generatedAtUtc` | string | yes | 生成時刻 |
| `sourceInputsHash` | string | yes | 入力ハッシュ |
| `entries` | array | yes | schema エントリ一覧 |

#### `schemas.catalog.json entries[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `schemaKey` | string | yes | `comp:<typeId>` または `asset:<typeId>` |
| `kind` | `comp \| asset` | yes | schema 種別 |
| `typeId` | string | yes | 型識別子 |
| `displayName` | string | yes | 表示名 |
| `properties` | array | yes | プロパティ一覧 |

#### `schemas.catalog.json entries[].properties[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `path` | string | yes | SerializedProperty path |
| `propertyType` | string | yes | `SerializedPropertyType` と意味対応する enum literal |
| `declaredTypeId` | `string \| null` | yes | 宣言型識別子 |
| `isArray` | boolean | yes | 配列か |
| `elementTypeId` | `string \| null` | yes | `isArray=true` のときのみ非 `null` |
| `isReadOnly` | boolean | yes | 読み取り専用か |

#### `asset-search.lookup.json`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `schemaVersion` | integer | yes | lookup schema version |
| `generatedAtUtc` | string | yes | 生成時刻 |
| `sourceInputsHash` | string | yes | `assetSearchHash` と一致する入力ハッシュ |
| `entries` | array | yes | asset 検索エントリ一覧 |

#### `asset-search.lookup.json entries[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `assetPath` | string | yes | `Assets/` 配下の persistent main asset path |
| `assetGuid` | string | yes | asset GUID |
| `name` | string | yes | main asset 名 |
| `typeId` | string | yes | runtime 型識別子 |
| `searchTypeIds` | array | yes | runtime 型から `UnityEngine.Object` までの型識別子連鎖 |

#### `guid-path.lookup.json`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `schemaVersion` | integer | yes | lookup schema version |
| `generatedAtUtc` | string | yes | 生成時刻 |
| `sourceInputsHash` | string | yes | `guidPathHash` と一致する入力ハッシュ |
| `entries` | array | yes | GUID-path エントリ一覧 |

#### `guid-path.lookup.json entries[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `assetGuid` | string | yes | asset GUID |
| `assetPath` | string | yes | `Assets/` 配下の persistent main asset path |

#### `lookups/scene-tree-lite/*.lookup.json`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `schemaVersion` | integer | yes | lookup schema version |
| `generatedAtUtc` | string | yes | 生成時刻 |
| `scenePath` | string | yes | persisted scene asset を指す project-relative path |
| `sourceInputsHash` | string | yes | 対象 Scene 本体と `.meta` の内容ハッシュ |
| `roots` | array | yes | persisted preview として読んだ Scene hierarchy の root node 一覧 |

#### `lookups/scene-tree-lite/*.lookup.json roots[]`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `name` | string | yes | GameObject 名 |
| `globalObjectId` | string | yes | 解決できたときの GlobalObjectId。解決不能時は空文字列 |
| `children` | array | yes | 子 node 一覧 |
| `childrenState` | `complete \| notExpandedByDepth \| truncatedByWindow \| unknown` | yes | 子 node 一覧の完全性 |

#### `inputs/manifest.json`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `schemaVersion` | integer | yes | manifest schema version |
| `generatedAtUtc` | string | yes | 生成時刻 |
| `scriptAssembliesHash` | string | yes | ScriptAssemblies ハッシュ |
| `packagesManifestHash` | string | yes | `Packages/manifest.json` ハッシュ |
| `packagesLockHash` | string | yes | `Packages/packages-lock.json` ハッシュ |
| `assemblyDefinitionHash` | string | yes | `.asmdef/.asmref` ハッシュ |
| `assetsContentHash` | string | yes | `Assets/` 配下の asset 本体と `.meta` の内容ハッシュ |
| `assetSearchHash` | string | yes | `asset-search.lookup.json` の鮮度判定に使う結合ハッシュ |
| `guidPathHash` | string | yes | `guid-path.lookup.json` の鮮度判定に使う結合ハッシュ |
| `combinedHash` | string | yes | 結合ハッシュ |

### `ucli test run`

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `result` | `pass \| fail \| null` | yes | テスト結果。`status=error` のときは `null` |
| `errorKind` | `invalidInput \| infraError \| toolError \| null` | yes | 失敗種別 |
| `runId` | string | yes | 実行 ID |
| `artifactsDir` | string | yes | 成果物ディレクトリ |
| `summaryJsonPath` | string | yes | サマリー JSON のパス |

### テストプロファイル JSON

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `schemaVersion` | integer | yes | profile schema version |
| `projectPath` | string | yes | 対象 Unity project path |
| `unityVersion` | `string \| null` | yes | Unity バージョン |
| `unityEditorPath` | `string \| null` | yes | Unity Editor 実行ファイルまたはディレクトリ |
| `testPlatform` | `editmode \| playmode \| <BuildTarget>` | yes | テスト実行先。`playmode` は現在対象の PlayMode、`<BuildTarget>` は player 実行先 |
| `testFilter` | `string \| null` | yes | テスト名フィルタ |
| `testCategories` | array | yes | テストカテゴリ一覧 |
| `assemblyNames` | array | yes | テストアセンブリ一覧 |
| `testSettingsPath` | `string \| null` | yes | `TestSettings.json` のパス |
| `timeout` | integer | yes | タイムアウトミリ秒数 |
