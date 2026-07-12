# Downloads the MobileSAM ONNX models used by the product visualizer.
#
# Source: Hugging Face repo "vietanhdev/segment-anything-onnx-models" (from the
# samexporter project, Apache-2.0) - https://huggingface.co/vietanhdev/segment-anything-onnx-models
#
# Unlike a plain file listing, this repo ships one zip per model variant. The
# MobileSAM zip is "mobile_sam_20230629.zip" (~36.7 MB) and contains:
#   - mobile_sam.encoder.onnx           (MobileSAM image encoder, ~28 MB)
#   - sam_vit_h_4b8939.decoder.onnx     (shared SAM mask decoder, ~16.5 MB)
# The decoder is named after the ViT-H variant because the SAM mask decoder is
# a small, backbone-agnostic network reused across all SAM/MobileSAM exports in
# this repo (confirmed via the zip's own config.yaml, which pairs the two files
# above for the "mobile_sam_20230629" model). This is expected, not a mistake.
#
# This script downloads the zip, extracts just those two files, renames them to
# the stable names the app expects, and removes the temporary zip.

$ErrorActionPreference = "Stop"

$repo = "https://huggingface.co/vietanhdev/segment-anything-onnx-models/resolve/main"
$zipFile = "mobile_sam_20230629.zip"          # <-- confirm against the repo listing
$encoderEntry = "mobile_sam.encoder.onnx"      # <-- confirm against the zip's config.yaml
$decoderEntry = "sam_vit_h_4b8939.decoder.onnx" # <-- confirm against the zip's config.yaml

$target = Join-Path $PSScriptRoot "..\src\NaturalStoneImpex.Api\MLModels"
New-Item -ItemType Directory -Force -Path $target | Out-Null

$tempZip = Join-Path ([System.IO.Path]::GetTempPath()) "mobile_sam_20230629.zip"
$tempExtract = Join-Path ([System.IO.Path]::GetTempPath()) ("mobile_sam_extract_" + [System.Guid]::NewGuid())

Write-Host "Downloading $zipFile ..."
Invoke-WebRequest "$repo/$zipFile" -OutFile $tempZip

Write-Host "Extracting models ..."
Expand-Archive -Path $tempZip -DestinationPath $tempExtract -Force

Copy-Item (Join-Path $tempExtract $encoderEntry) (Join-Path $target "mobilesam-encoder.onnx") -Force
Copy-Item (Join-Path $tempExtract $decoderEntry) (Join-Path $target "mobilesam-decoder.onnx") -Force

Remove-Item $tempZip -Force
Remove-Item $tempExtract -Recurse -Force

Write-Host "Models downloaded to $target"

# Fallback if the repo layout changed: pip install samexporter and export from the
# MobileSAM checkpoint per https://github.com/vietanhdev/samexporter#usage, e.g.:
#   python -m samexporter.export_encoder --checkpoint original_models/mobile_sam.pt `
#       --output output_models/mobile_sam.encoder.onnx --model-type mobile_sam --use-preprocess
#   python -m samexporter.export_decoder --checkpoint original_models/mobile_sam.pt `
#       --output output_models/mobile_sam.decoder.onnx --model-type mobile_sam --return-single-mask
# Then copy the outputs to MLModels\mobilesam-encoder.onnx / mobilesam-decoder.onnx.
