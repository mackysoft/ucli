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
- `argsSchema` / `resultSchema` は JSON 構造検証に使い、operation 選択、入力構築、結果解釈は `description`、`inputs[].constraints`、`inputs[].variants[].fields[].constraints`、`resultContract`、`assurance` を主契約として扱う。
- 任意 C# 実行、任意 shell 実行、Unity YAML 直編集、`--allowDangerous` が必要な operation は通常導線に含めない。

## ディレクトリ方針
| Path | 役割 | 手編集 |
| --- | --- | --- |
| `src/Ucli.Skills/SkillDefinitions/` | 人間が編集する SKILL 文言、template、metadata の定義 | 可 |
| `skills/` | canonical generated output。package、release、install、export の配布元 | 原則不可 |
| host install target | agent host が読む project-local または user-local の配置先 | `ucli skills` が配置 |

`src/Ucli.Skills/SkillDefinitions/` は SKILL 文言と host 非依存 metadata の入力である。

```text
src/Ucli.Skills/
  SkillDefinitions/
    ucli-plan-apply/
      skill.json
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

1. `src/Ucli.Skills/SkillDefinitions/` から `skills/` を生成する。
2. `ucli skills install/export --host <host>` が `skills/` から host 形式へ materialize する。

公式 SKILL は、uCLI が supported host として定義する全 host へ materialize できなければならない。host 対応は skill ごとの metadata ではなく、`src/Ucli.Skills` の host adapter set で管理する。

supported host は次のとおり。

| Host | Project scope target | User scope target | Reload guidance | 備考 |
| --- | --- | --- | --- | --- |
| `claude` | `.claude/skills` | `~/.claude/skills` | 既存 skill directory は Claude Code が監視する。top-level directory をセッション後に作成した場合は Claude Code を再起動する。 | Claude Code project / personal skill |
| `copilot` | `.github/skills` | `~/.copilot/skills` | GitHub Copilot CLI で `/skills reload` を実行する。 | GitHub Copilot CLI project / personal skill |
| `openai` | `.agents/skills` | `${CODEX_HOME}/skills`、未設定時 `~/.codex/skills` | Codex session または app を再起動する。 | OpenAI / Codex 向け metadata を `agents/openai.yaml` として含める |

`SKILL.md` 本文と `references/` の内容は host で変えない。host で変えるのは次だけとする。

- `SKILL.md` frontmatter の host 固有 field
- host 固有 metadata
- install / export target path
- reload / restart guidance

`install`、`update`、`uninstall`、`export`、`doctor` は、配置先だけでなく host 固有 metadata を決めるために `--host <host>` を明示入力として扱う。`--targetDir` が指定される場合でも、materialize 形式を決めるために host は必要である。各 command payload は host-specific reload / restart guidance を返す。

対応していない host は option 正規化時に失敗させる。`--targetDir` は host support validation を迂回しない。uCLI は unsupported host を generic skill output へ fallback しない。host を追加する場合は、host adapter、project / user target path、metadata policy、reload guidance、doctor rule、materialization test、export test を追加する。

host materialization は決定論的でなければならない。

- 同じ `skills/`、同じ host、同じ option からは byte-identical な出力を生成する。
- `SKILL.md` body は canonical generated output と一致させる。
- host adapter が変更できるのは frontmatter、host metadata、install / reload guidance だけとする。
- host adapter は operation catalog、request schema、operation args の説明を挿入しない。

`ucli skills export` は `--format directory|zip` を受け付ける。`directory` は現行どおり `<output>/<skillName>/...` を書き出し、`zip` は release artifact 用に `<skillName>/<relativePath>` を zip root に並べる。zip entry は ordinal sort、directory entry なし、固定 timestamp、UTF-8 LF content で生成し、同じ input / host / option から byte-identical な zip を出力する。

OpenAI / Codex 向け metadata は、OpenAI host adapter が各 skill の `agents/openai.yaml` として生成する。

```yaml
interface:
  display_name: "uCLI Plan Apply"
  short_description: "Validate, plan, and apply uCLI requests"
  default_prompt: "Use $ucli-plan-apply to validate, plan, and apply a uCLI JSON request."

policy:
  allow_implicit_invocation: true
```

Claude 向けの `disable-model-invocation`、`allowed-tools`、`paths` などは、Claude host adapter が必要に応じて `SKILL.md` frontmatter へ materialize する。

`allowed-tools` は tool 使用の事前承認であり、安全境界ではない。任意コード実行や `--allowDangerous` が必要な operation の制御は、uCLI 側の operation policy、allowlist、`--allowDangerous` guard を正とする。

Claude SDK のように `SKILL.md` frontmatter の `allowed-tools` が効かない実行環境では、SDK 側の tool allowlist や permission 設定を別途使う。

## `skill.json` Schema
`src/Ucli.Skills/SkillDefinitions/<skill>/skill.json` は、template rendering と host artifact 生成の入力である。uCLI は source metadata を JSON として読み、YAML parser 依存を持たない。最小 schema は次のとおり。

```json
{
  "schemaVersion": 1,
  "skillName": "ucli-plan-apply",
  "displayName": "uCLI Plan Apply",
  "description": "Use when validating, planning, and applying uCLI JSON requests.",
  "references": [
    "request-workflow.md",
    "failure-semantics.md"
  ]
}
```

`skill.json` は host 非依存 metadata だけを持つ。host ごとの frontmatter、metadata、出力先は host adapter が生成する。公式 SKILL は全 supported host へ出力するため、skill ごとの host allowlist は持たない。

`ucli skills install`、`ucli skills update`、`ucli skills uninstall`、`ucli skills export` は、公式 SKILL を常に一括で扱う。SKILL 種別ごとの導入分けは 1.0 の仕様に含めない。公式 SKILL として含めるには、全 supported host に対して安全に一括導入できることを条件にする。

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
- `ucli codes describe <CODE>` を、失敗後の次行動や保証不足を決める台帳として参照する指示
- `--allowDangerous` を通常導線にしない規則

含めない内容:

- operation catalog の丸写し
- code catalog の丸写し
- `argsSchema` / `resultSchema` の手書き説明
- README や command reference のコピー
- 任意 C# 実行、任意 shell 実行、Unity YAML 直編集を便利導線として扱う説明
- `--allowDangerous` が必要な operation を通常 workflow example に含める説明
- host ごとの長い install 手順

`SKILL.md` は 500 lines 未満を原則とする。詳細な補助説明は `references/` に分離し、`SKILL.md` から「いつ読むか」を明示する。

## References
`references/` は、`SKILL.md` から必要時だけ参照する補助 workflow 文書である。agent の初期 context に常に載せる本文ではなく、長くなりやすい判断基準、失敗時の読み方、出力解釈を分離するために使う。

`references/` にも operation catalog、args/result schema、README、command reference の丸写しは置かない。operation の詳細は常に `ucli ops describe <opName>` を正本とする。

## Official Skills
公式 SKILL は一括で install / export される。個別導入、SKILL 種別ごとの導入分け、host ごとの対応可否は 1.0 の仕様に含めない。

| Skill | 役割 |
| --- | --- |
| `ucli-read-project` | Unity project の状態、selector、schema、operation metadata を読む |
| `ucli-plan-apply` | JSON request を構築し、`validate` / `plan` / `call` で適用する |
| `ucli-verify-changes` | `verify --from`、必要な `test`、`logs`、再 query で変更後の claim と証跡を確認する |
| `ucli-troubleshoot` | daemon、lifecycle、readIndex、timeout、compile / domain reload を診断する |

uCLI operation authoring 支援は初期の公式 SKILL には含めない。利用者の Unity project を操作する workflow と、uCLI 本体の operation contract / 実装を変更する workflow は責務が違うため、将来追加する場合も別の公式 SKILL として設計する。

公式 SKILL は通常 workflow example に `--allowDangerous` を含めない。任意 C# 実行、任意 shell 実行、Unity YAML 直編集、`--allowDangerous` が必要な operation は公式 SKILL の通常 workflow に含めない。

## Scripts Policy
公式 SKILL に executable script を含めない。

## Manifest と Digest
各 generated skill は `ucli-skill.json` を持つ。

```json
{
  "schemaVersion": 1,
  "skillName": "ucli-plan-apply",
  "displayName": "uCLI Plan Apply",
  "description": "Build, validate, plan, apply, and verify uCLI JSON requests.",
  "contentDigest": "sha256:...",
  "hostArtifacts": [
    {
      "host": "openai",
      "path": "agents/openai.yaml",
      "digest": "sha256:...",
      "materializedFrontmatterDigest": "sha256:..."
    },
    {
      "host": "claude",
      "materializedFrontmatterDigest": "sha256:..."
    },
    {
      "host": "copilot",
      "materializedFrontmatterDigest": "sha256:..."
    }
  ]
}
```

`ucli-skill.json` は canonical manifest として扱い、install 後も内容を変えない。

`displayName` と `description` は listing と host metadata 生成に使う canonical metadata とする。`contentDigest` は `SKILL.md` と host 非依存 `references/` の内容から算出する。`displayName`、`description`、`ucli-skill.json`、host 固有 artifact は `contentDigest` に含めない。

host 固有 artifact の digest は `hostArtifacts` で別枠にし、OpenAI 用 `agents/openai.yaml` などの drift を共通本文の drift と混ぜない。`materializedFrontmatterDigest` は各 host の materialized `SKILL.md` frontmatter を検証し、`digest` は metadata artifact file を持つ host だけに付与する。

Digest input は次の規則で正規化する。

- text は UTF-8 と LF 改行へ正規化する。
- path は `/` 区切りに正規化する。
- file list は path の ordinal 昇順に並べる。
- directory entry は digest input に含めない。
- `ucli-skill.json`、manifest metadata、host 固有 artifact は `contentDigest` に含めない。
- digest input は `path + NUL + content` の列として構成し、path と content の境界を曖昧にしない。

installed skill directory に個別の install metadata file は置かない。install 状態は host target 配下の `ucli-skill.json` を scan して判定する。

複数 host へ install する場合は、`host + scope + targetRoot + skillName` を install identity とする。同じ skill 名でも host が異なれば別 install として扱う。installed skill を対象にする command は指定 host の target directory だけを scan し、別 host の target は変更しない。

同じ target root を複数 host で共有する install は衝突として扱い、拒否する。host 固有 frontmatter や `agents/openai.yaml` の有無が混ざると、materialized output の対象 host を安全に判定できないためである。

## Install Safety
`install` は、指定 host の target に公式 SKILL を一括で配置する。既存 target は暗黙上書きしない。project scope は `--repoRoot` を必須とし、user scope は host 既定 user target を使う。user scope に `--targetDir` を指定する場合は absolute path に限定し、`--repoRoot` は受け付けない。

- target skill directory が存在しない場合は新規作成する。
- target skill directory が存在し、`ucli-skill.json` の `contentDigest` が一致する場合は no-op とする。
- target skill directory が存在し、`contentDigest` が一致しない場合は失敗する。
- target skill directory が存在するが `ucli-skill.json` が無い場合は失敗する。

path safety は次の規則で扱う。

- `--targetDir` は canonical absolute path に正規化してから検証する。
- project scope の install target は repository root 配下に限定する。
- user scope の install target は host 既定 user target または absolute `--targetDir` に限定する。
- `..` や symlink により repository root または target root の外へ出る path は拒否する。
- materialized artifact の各 file path が target root 外へ出ないことを検証する。
- official SKILL に executable file が含まれる場合は scripts policy 違反として失敗する。

## Update / Uninstall Safety
`update` は、指定 host の target にある公式 SKILL を一括で最新化する。target root または公式 skill directory が無い場合は作成し、導入済み内容が最新なら no-op とする。

- target skill directory が存在しない場合は新規作成する。
- target skill directory が存在し、現在の bundled official SKILL と一致する場合は no-op とする。
- target skill directory が存在し、導入済み `ucli-skill.json` 自身の digest と host artifact に対して clean な場合だけ更新する。
- target skill directory が存在するが `ucli-skill.json` が無い場合は unmanaged として失敗し、上書きしない。
- local modification、別 host materialization、manifest 不正は失敗し、`--force` なしでは更新しない。

`uninstall` は、指定 host の target 配下にある uCLI 管理済み公式 SKILL だけを削除する。target root 自体、別 host の target、unrelated directory は削除しない。

- target root または target skill directory が存在しない場合は no-op とする。
- `ucli-skill.json` が無い target skill directory は unmanaged として削除せず、payload で skipped として返す。
- bundled official SKILL と照合でき、指定 host の materialized output と一致し、local modification が無い場合だけ削除する。
- local modification、別 host materialization、manifest 不正は失敗し、`--force` なしでは削除しない。

## 責務境界
| 領域 | 責務 |
| --- | --- |
| `src/Ucli.Skills/SkillDefinitions/` | 人間が編集する template と metadata |
| `skills/` | generated canonical output |
| `src/Ucli.Skills` | `SkillDefinitions/`、SKILL 生成、検証、manifest、digest、host adapter、export、install、update、uninstall、doctor の中核ロジック |
| `src/Ucli` | `ucli skills` CLI entrypoint、option 正規化、`CommandResult` 生成 |
| `src/Ucli.Contracts` | IPC、operation、JSON request / result の契約。SKILL manifest は置かない |
| `src/Ucli.Infrastructure` | 汎用 filesystem、hash、path helper。SKILL 固有ポリシーは置かない |

`src/Ucli.Skills` は原則として `src/Ucli.Contracts` に依存しない。`Ucli.Skills` は operation contract を反射・再定義しない。SKILL は operation catalog を含まないため、operation の正確な args、result、assurance は実行時に `ucli ops describe` から取得する。

## Doctor Scope
`ucli skills doctor` は SKILL 配布物だけを診断する。対象は target directory、host adapter、`SKILL.md`、`ucli-skill.json`、digest、supported host artifact の有無とする。doctor は common content drift、frontmatter drift、host artifact drift、file-set drift、clean outdated を別の diagnostic code として返す。OpenAI / Codex の `agents/openai.yaml` drift は common content drift ではなく host artifact drift として扱う。

Unity plugin、daemon、project status は `ucli status` や既存の daemon / logs command に委ねる。

## CI 方針
CI は SKILL を正本化させないための drift gate として扱う。

- `src/Ucli.Skills/SkillDefinitions/` から `skills/` を再生成し、差分があれば失敗する。
- `ucli-skill.json` の必須 field と digest を検証する。
- `hostArtifacts` が全 supported host の artifact を持つことを検証する。
- `hostArtifacts` の digest を検証する。
- 公式 SKILL に `scripts/` または executable file が含まれていたら失敗する。
- `SKILL.md` に `ucli ops describe` 誘導が無い場合は失敗する。
- 任意 C# 実行、任意 shell 実行、Unity YAML 直編集、`--allowDangerous` が必要な operation が通常 workflow として書かれていたら失敗する。
- `SKILL.md` が 500 lines 以上になった場合は分割を要求する。
- `references/*.md` が 1000 lines 以上になった場合は分割を要求する。
- `SKILL.md` と `references/` に operation catalog の丸写しが含まれていないことを検査する。
