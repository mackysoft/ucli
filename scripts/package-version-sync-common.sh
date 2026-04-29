#!/usr/bin/env bash

parse_package_version_arg() {
  local usage_function="$1"
  shift

  local package_version=""
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --version)
        [[ $# -ge 2 ]] || { "${usage_function}"; exit 2; }
        package_version="$2"
        shift 2
        ;;
      -h|--help)
        "${usage_function}"
        exit 0
        ;;
      *)
        "${usage_function}"
        exit 2
        ;;
    esac
  done

  if [[ -z "${package_version}" ]]; then
    "${usage_function}"
    exit 2
  fi

  require_semver_package_version "${package_version}"
  printf '%s\n' "${package_version}"
}

require_semver_package_version() {
  local package_version="$1"

  if [[ ! "${package_version}" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "Package version must use <major>.<minor>.<patch> format. Actual: ${package_version}" >&2
    exit 1
  fi
}

read_required_xml_element_value() {
  local file_path="$1"
  local element_name="$2"
  local description="$3"

  local current_value
  current_value="$(
    ELEMENT_NAME="${element_name}" perl -ne '
      my $element = $ENV{"ELEMENT_NAME"};
      if (/<\Q$element\E>([^<]+)<\/\Q$element\E>/) {
        print $1;
        exit;
      }
    ' "${file_path}"
  )"

  if [[ -z "${current_value}" ]]; then
    echo "Failed to resolve ${description} from ${file_path}." >&2
    exit 1
  fi

  printf '%s\n' "${current_value}"
}

read_required_xml_attribute_value() {
  local file_path="$1"
  local attribute_prefix="$2"
  local description="$3"

  local current_value
  current_value="$(
    ATTRIBUTE_PREFIX="${attribute_prefix}" perl -ne '
      my $prefix = $ENV{"ATTRIBUTE_PREFIX"};
      if (/\Q$prefix\E([^"]+)"/) {
        print $1;
        exit;
      }
    ' "${file_path}"
  )"

  if [[ -z "${current_value}" ]]; then
    echo "Failed to resolve ${description} from ${file_path}." >&2
    exit 1
  fi

  printf '%s\n' "${current_value}"
}

update_xml_element_value() {
  local file_path="$1"
  local element_name="$2"
  local package_version="$3"
  local description="$4"

  local current_value
  current_value="$(read_required_xml_element_value "${file_path}" "${element_name}" "${description}")"
  if [[ "${current_value}" == "${package_version}" ]]; then
    return 0
  fi

  ELEMENT_NAME="${element_name}" PACKAGE_VERSION="${package_version}" perl -0pi -e '
    my $element = $ENV{"ELEMENT_NAME"};
    my $version = $ENV{"PACKAGE_VERSION"};
    s{(<\Q$element\E>)[^<]+(</\Q$element\E>)}{$1$version$2};
  ' "${file_path}"
}

update_xml_attribute_value() {
  local file_path="$1"
  local attribute_prefix="$2"
  local package_version="$3"
  local description="$4"

  local current_value
  current_value="$(read_required_xml_attribute_value "${file_path}" "${attribute_prefix}" "${description}")"
  if [[ "${current_value}" == "${package_version}" ]]; then
    return 0
  fi

  ATTRIBUTE_PREFIX="${attribute_prefix}" PACKAGE_VERSION="${package_version}" perl -0pi -e '
    my $prefix = $ENV{"ATTRIBUTE_PREFIX"};
    my $version = $ENV{"PACKAGE_VERSION"};
    s{(\Q$prefix\E)[^"]+(")}{$1$version$2};
  ' "${file_path}"
}
