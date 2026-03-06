{{- define "worker.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "worker.fullname" -}}
{{- printf "%s-%s" .Release.Name (include "worker.name" .) | trunc 63 | trimSuffix "-" -}}
{{- end -}}
