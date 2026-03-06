{{- define "broker.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "broker.fullname" -}}
{{- printf "%s-%s" .Release.Name (include "broker.name" .) | trunc 63 | trimSuffix "-" -}}
{{- end -}}
