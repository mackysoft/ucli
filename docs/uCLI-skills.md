# uCLI Skills

> [!IMPORTANT]
> この文書は、uCLI 公式 SKILL の仕様、生成方針、責務境界、配布運用を定義する。
> ここで扱う SKILL は agent 向けの workflow layer であり、uCLI operation や JSON request の仕様正本ではない。

## 基本方針
- SKILL を仕様の正本にしない。
- 仕様の正本は `Ucli.Contracts`、operation metadata、`ucli ops describe`、[json-request-spec.md](json-request-spec.md) に置く。
- SKILL は agent に安全な作業順序を教える薄い workflow layer とする。
- SKILL には operation catalog、operation args、result schema、README、command reference の内容を手書きで複製しない。
- primitive operation を使う前に、agent は `ucli ops describe <opName>` を読む。
- `argsSchema` / `resultSchema` は JSON 構造検証に使い、operation 選択、入力構築、結果解釈は `description`、`inputs[].constraints`、`resultContract`、`assurance` を主契約として扱う。
- dangerous operation と任意コード実行は通常導線に含めない。`--allowDangerous` はユーザーが明示した場合だけ扱う。

## ディレクトリ方針
| Path | 役割 | 手編集 |
| --- | --- | --- |
| `skills-src/` | 人間が編集する SKILL 文言、template、metadata の正本 | 可 |
| `skills/` | canonical generated output。package、release、install、export の配布元 | 原則不可 |
| host install target | agent host が読む project-local または user-local の配置先 | `ucli skills` が配置 |

`skills-src/` は SKILL 文言と host 非依存 metadata の入力である。

```text
skills-src/
  ucli-plan-apply/
    skill.yaml
    SKILL.md.template
    references/
      request-workflow.md.template
```

`skills/` は生成済みの配布物であり、CLI package と release artifact に含める。

```text
skills/
  ucli-plan-apply/
    SKILL.md
    ucli-skill.json
    references/
      request-workflow.md
    agents/
      openai.yaml
```

install / export 先は host によって変わる。

```text
.claude/skills/
.github/skills/
.agents/skills/
```

## 生成方針
生成は2段階に分ける。

1. `skills-src/` から `skills/` を生成する。
2. `ucli skills install/export --host <host>` が `skills/` から host 形式へ materialize する。

`SKILL.md` 本文と `references/` の内容は host で変えない。host で変えるのは次だけとする。

- `SKILL.md` frontmatter の host 固有 field
- host 固有 metadata
- install / export target path
- reload / restart guidance

`install`、`export`、`doctor` は、配置先だけでなく host 固有 metadata を決めるために `--host <host>` を明示入力として扱う。`--targetDir` が指定される場合でも、materialize 形式を決めるために host は必要である。

対応していない host は option 正規化時に失敗させる。`--targetDir` は host support validation を迂回しない。uCLI は unsupported host を generic skill output へ fallback しない。host を追加する場合は、host adapter、target path、metadata policy、doctor rule、materialization test を追加する。

host materialization は決定論的でなければならない。

- 同じ `skills/`、同じ host、同じ option からは byte-identical な出力を生成する。
- `SKILL.md` body は canonical generated output と一致させる。
- host adapter が変更できるのは frontmatter、host metadata、install / reload guidance だけとする。
- host adapter は operation catalog、request schema、operation args の説明を挿入しない。

OpenAI / Codex 向け metadata は、各 skill の `agents/openai.yaml` として生成する。

```yaml
interface:
  display_name: "uCLI Plan Apply"
  short_description: "Validate, plan, and apply uCLI requests"
  default_prompt: "Use $ucli-plan-apply to validate, plan, and apply a uCLI JSON request."

policy:
  allow_implicit_invocation: true
```

Claude 向けの `disable-model-invocation`、`allowed-tools`、`paths` などは、Claude host adapter が必要に応じて `SKILL.md` frontmatter へ materialize する。

`allowed-tools` は tool 使用の事前承認であり、安全境界ではない。dangerous operation の制御は、uCLI 側の operation policy、allowlist、`--allowDangerous` guard を正とする。

Claude SDK のように `SKILL.md` frontmatter の `allowed-tools` が効かない実行環境では、SDK 側の tool allowlist や permission 設定を別途使う。

## `skill.yaml` Schema
`skills-src/<skill>/skill.yaml` は、template rendering と host artifact 生成の入力である。最小 schema は次のとおり。

```yaml
schemaVersion: 1
skillName: ucli-plan-apply
displayName: uCLI Plan Apply
description: >
  Use when validating, planning, and applying uCLI JSON requests.
skillSet: core
defaultIncluded: true
implicitInvocation: true
dangerousAllowed: false
userInvokedOnly: false
references:
  - request-workflow.md
  - failure-semantics.md
hosts:
  openai:
    allowImplicitInvocation: true
  claude:
    disableModelInvocation: false
```

`dangerousAllowed` が `true` の skill は core set に入れない。`userInvokedOnly` は authoring skill や dangerous workflow を自動起動させないために使う。

## SKILL 文言
`SKILL.md.template` には agent の行動を制約する固定 workflow だけを書く。

含める内容:

- skill の利用条件を絞った `description`
- `ucli ops describe <opName>` を読む指示
- `read -> describe -> build request -> validate -> plan -> call -> verify` の順序
- bounded output の規則
- fixed sleep 禁止
- `IPC_TIMEOUT` を未適用と断定しない規則
- `payload.opResults[].applied`、`changed`、`touched` の確認
- `readPostcondition` が要求する mutation 後 read の扱い
- `--allowDangerous` を通常導線にしない規則

含めない内容:

- operation catalog の丸写し
- `argsSchema` / `resultSchema` の手書き説明
- README や command reference のコピー
- dangerous operation や任意 C# 実行を便利導線として扱う説明
- host ごとの長い install 手順

`SKILL.md` は 500 lines 未満を原則とする。詳細な補助説明は `references/` に分離し、`SKILL.md` から「いつ読むか」を明示する。

## Official Skill Set
| Skill | 役割 | 既定導入 |
| --- | --- | --- |
| `ucli-read-project` | Unity project の状態、selector、schema、operation metadata を読む | core |
| `ucli-plan-apply` | JSON request を構築し、`validate` / `plan` / `call` で適用する | core |
| `ucli-verify-changes` | `test`、`logs`、再 query で変更後の証跡を確認する | core |
| `ucli-troubleshoot` | daemon、lifecycle、readIndex、timeout、compile / domain reload を診断する | core |
| `ucli-author-operation` | uCLI 開発者が operation contract と実装を追加する | optional |

`ucli-author-operation` は利用者の Unity project を操作する skill と分ける。authoring skill は user-invoked 寄りにし、自動起動を抑制する。

dangerous / 任意 C# 実行用 skill は core set に入れない。

Official SKILL は通常 workflow example に `--allowDangerous` を含めない。dangerous workflow を文書化する場合は、別の non-core skill に分離し、`userInvokedOnly` として扱う。

## Scripts Policy
公式 SKILL に executable script を含めない。

## Manifest と Install State
各 generated skill は `ucli-skill.json` を持つ。

```json
{
  "schemaVersion": 1,
  "skillName": "ucli-plan-apply",
  "contentDigest": "sha256:...",
  "hostArtifacts": [
    {
      "host": "openai",
      "path": "agents/openai.yaml",
      "digest": "sha256:..."
    },
    {
      "host": "claude",
      "materializedFrontmatterDigest": "sha256:..."
    }
  ]
}
```

`ucli-skill.json` は canonical manifest として扱い、install 後も内容を変えない。

`contentDigest` は `SKILL.md` と host 非依存 `references/` の内容から算出する。host 固有 artifact の digest は `hostArtifacts` で別枠にし、OpenAI 用 `agents/openai.yaml` などの drift を共通本文の drift と混ぜない。

Digest input は次の規則で正規化する。

- text は UTF-8 と LF 改行へ正規化する。
- path は `/` 区切りに正規化する。
- file list は path の ordinal 昇順に並べる。
- directory entry は digest input に含めない。
- `ucli-skill.json`、`.ucli/skills.lock.json`、host 固有 artifact は `contentDigest` に含めない。
- digest input は `path + NUL + content` の列として構成し、path と content の境界を曖昧にしない。

Project-local install では `.ucli/skills.lock.json` を持つ。lock は project に install された SKILL の一覧であり、install 状態の正本である。

```json
{
  "schemaVersion": 1,
  "entries": [
    {
      "host": "claude",
      "scope": "project",
      "targetRoot": ".claude/skills",
      "skillName": "ucli-plan-apply",
      "contentDigest": "sha256:..."
    }
  ]
}
```

installed skill directory に個別の install metadata file は置かない。install 状態は `.ucli/skills.lock.json` で管理し、skill directory 側は canonical `SKILL.md`、`ucli-skill.json`、`references/`、host 固有 artifact だけを持つ。

複数 host へ install する場合は、`host + scope + targetRoot + skillName` を install identity とする。同じ skill 名でも host が異なれば別 install として扱い、lock も host ごとの entry を持つ。installed skill を対象にする command は指定 host の install だけを対象にし、別 host の target は変更しない。

同じ target root を複数 host で共有する install は衝突として扱い、拒否する。host 固有 frontmatter や `agents/openai.yaml` の有無が混ざると、materialized output の対象 host を安全に判定できないためである。

## Install Safety
host ごとの既定 target は次のとおり。

| Host | Project scope target | 備考 |
| --- | --- | --- |
| `claude` | `.claude/skills` | Claude Code project skill |
| `copilot` | `.github/skills` | GitHub Copilot CLI project skill |
| `openai` | `.agents/skills` | OpenAI / Codex 向け metadata を `agents/openai.yaml` として含める |

`install` は既存 target を暗黙上書きしない。

- target skill directory が存在しない場合は新規作成する。
- target skill directory が存在し、`ucli-skill.json` の `contentDigest` が一致する場合は no-op とする。
- target skill directory が存在し、`contentDigest` が一致しない場合は失敗する。
- target skill directory が存在するが `ucli-skill.json` が無い場合は失敗する。

path safety は次の規則で扱う。

- `--targetDir` は canonical absolute path に正規化してから検証する。
- project scope の install target は repository root 配下に限定する。
- `..` や symlink により repository root または target root の外へ出る path は拒否する。
- materialized artifact の各 file path が target root 外へ出ないことを検証する。
- official SKILL に executable file が含まれる場合は scripts policy 違反として失敗する。

## 責務境界
| 領域 | 責務 |
| --- | --- |
| `skills-src/` | 人間が編集する template と metadata |
| `skills/` | generated canonical output |
| `src/Ucli.Skills` | SKILL 生成、検証、manifest、digest、host adapter、export、install、doctor の中核ロジック |
| `src/Ucli` | `ucli skills` CLI entrypoint、option 正規化、`CommandResult` 生成 |
| `src/Ucli.Contracts` | IPC、operation、JSON request / result の契約。SKILL manifest は置かない |
| `src/Ucli.Infrastructure` | 汎用 filesystem、hash、path helper。SKILL 固有ポリシーは置かない |

`src/Ucli.Skills` は原則として `src/Ucli.Contracts` に依存しない。`Ucli.Skills` は operation contract を反射・再定義しない。SKILL は operation catalog を含まないため、operation の正確な args、result、assurance は実行時に `ucli ops describe` から取得する。

## Doctor Scope
`ucli skills doctor` は SKILL 配布物だけを診断する。対象は target directory、host adapter、`SKILL.md`、`ucli-skill.json`、lock、digest、dangerous core skill の有無とする。

Unity plugin、daemon、project status は `ucli status` や既存の daemon / logs command に委ねる。

## CI 方針
CI は SKILL を正本化させないための drift gate として扱う。

- `skills-src/` から `skills/` を再生成し、差分があれば失敗する。
- `ucli-skill.json` の必須 field と digest を検証する。
- `hostArtifacts` の digest を検証する。
- `.ucli/skills.lock.json` と install target の整合性を検証する。
- 公式 SKILL に `scripts/` または executable file が含まれていたら失敗する。
- `SKILL.md` に `ucli ops describe` 誘導が無い場合は失敗する。
- dangerous operation が通常 workflow として書かれていたら失敗する。
- `SKILL.md` が 500 lines 以上になった場合は分割を要求する。
- `references/*.md` が 1000 lines 以上になった場合は分割を要求する。
- `references/` に operation catalog の丸写しが含まれていないことを検査する。
