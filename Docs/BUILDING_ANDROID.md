# Сборка Android APK

Проект собирается на Unity `2022.3.46f1` с Android Build Support, SDK/NDK и OpenJDK.

## Локально

1. Откройте проект в Unity Hub.
2. Убедитесь, что для Unity 2022.3.46f1 установлены Android Build Support, Android SDK & NDK Tools и OpenJDK.
3. В Unity выберите **Путь Кольца → Собрать Android APK**.
4. Готовый файл: `Builds/PutKoltsa.apk`.

Сборщик автоматически создаёт игровую сцену, добавляет её в Build Settings и выставляет:

- package ID: `com.fanprototype.putkoltsa`;
- version: `0.1.0`;
- minimum Android: API 23;
- orientation: landscape;
- architectures: ARMv7 и ARM64;
- scripting backend: IL2CPP.

## GitHub Actions

Workflow `.github/workflows/build-android.yml` запускается вручную или при push в `main`.

Перед первым запуском добавьте в **Settings → Secrets and variables → Actions** данные активации Unity:

- `UNITY_LICENSE` — содержимое файла лицензии Unity;
- `UNITY_EMAIL` и `UNITY_PASSWORD` — если они требуются выбранным типом активации GameCI.

После успешного workflow APK доступен в разделе **Artifacts** под именем `PutKoltsa-Android`.

Секреты Unity нельзя сохранять в проекте, коммитах или логах.
