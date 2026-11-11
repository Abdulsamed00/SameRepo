#!/usr/bin/env bash
# PreToolUse hook (Edit|Write|MultiEdit): NPC yürüme animasyonuyla ilgili dosyaları korur.
# 11.11: NPC yürüyüşündeki hatalar (klip hızı, ayak IK'sı, kaldırım/NavMesh) düzeltildi; bozulmasın diye bu dosyalara
# Claude Code'un dosya düzenleme araçlarıyla dokunulmaz. Unity MCP (RunCommand) ile yapılan değişiklikleri YAKALAMAZ.
# Çıkış: 2 = engelle (stderr Claude'a gider), 0 = izin ver.

input="$(cat)"

# tool_input.file_path: jq varsa jq ile, yoksa (bu makinede jq kurulu değil) Python ile okunur. İkisi de yoksa düz metin.
if command -v jq >/dev/null 2>&1; then
  file_path="$(printf '%s' "$input" | jq -r '.tool_input.file_path // empty')"
elif command -v python >/dev/null 2>&1 || command -v python3 >/dev/null 2>&1; then
  py="$(command -v python3 || command -v python)"
  file_path="$(printf '%s' "$input" | "$py" -c 'import sys, json
try:
    print(json.load(sys.stdin).get("tool_input", {}).get("file_path", "") or "")
except Exception:
    print("")')"
fi
# JSON okunamadıysa (ya da araç yoksa) düz metinden: koruma sessizce devre dışı kalmasın.
if [ -z "$file_path" ]; then
  file_path="$(printf '%s' "$input" | sed -n 's/.*"file_path"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -n1)"
fi

[ -z "$file_path" ] && exit 0

# Windows yolları: ters eğik çizgi -> düz (JSON'dan ham okunduysa çift \\ de tek / olur), başına / (göreli ve mutlak
# yollar aynı desenle eşleşsin).
path="/$(printf '%s' "$file_path" | tr '\\' '/' | tr -s '/')"

shopt -s nocasematch
protected=0
case "$path" in
  # Yürüme mantığı: hız/klip hızı (NpcCrowd, NpcAgent), ayak IK'sı ve zemine oturtma (NpcIk, NpcAgent).
  */Assets/Scripts/NPC/*) protected=1 ;;
  # Düzeltilmiş yürüme/idle klipleri.
  */Assets/Anims/Duzeltilmis/*) protected=1 ;;
  # NPC animator controller'ları (Man_Walk / Women_Walking state'leri).
  */Assets/Anims/NPC_Anims/NPC*.controller|*/Assets/Anims/NPC_Anims/NPC*.controller.meta) protected=1 ;;
  # NPC prefab'ları (NpcIk, kıyafet, controller bağları).
  */Assets/Prefabs/NPC*.prefab|*/Assets/Prefabs/NPC*.prefab.meta) protected=1 ;;
  # Yürüme kliplerinin kaynak FBX'leri (import ayarları: bake, döngü).
  */Npc_K*z/K*z_npc.fbx|*/Npc_K*z/K*z_npc.fbx.meta) protected=1 ;;
  */Erkek_Y*r*me_Ani.fbx|*/Erkek_Y*r*me_Ani.fbx.meta) protected=1 ;;
  # Kaldırım kenarı NavMesh alanı (Kaldirim_Kenari, maliyet 5).
  */ProjectSettings/NavMeshAreas.asset) protected=1 ;;
esac

if [ "$protected" -eq 1 ]; then
  echo "ENGELLENDİ: bu dosya NPC yürüme animasyonuyla ilgili ve korumalı. Dokunma, bunu atlayıp görevin geri kalanına devam et. ($file_path)" >&2
  exit 2
fi
exit 0
