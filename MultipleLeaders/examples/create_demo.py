"""Run inside ArcGIS Pro's Python window with runpy.run_path(..., run_name='__main__')."""

from pathlib import Path
from uuid import uuid4

import arcpy


def main():
    # CURRENT intentionally requires Pro. Fail before writing files if run elsewhere.
    project = arcpy.mp.ArcGISProject("CURRENT")
    output = Path(__file__).resolve().parent / "output" / uuid4().hex[:12]
    output.mkdir(parents=True, exist_ok=False)
    with arcpy.EnvManager(overwriteOutput=False):
        geodatabase = str(arcpy.management.CreateFileGDB(str(output), "demo.gdb").getOutput(0))
        points = str(arcpy.management.CreateFeatureclass(
            geodatabase, "DemoPoints", "POINT", spatial_reference=arcpy.SpatialReference(3857)
        ).getOutput(0))
        arcpy.management.AddField(points, "GROUP_NAME", "TEXT", field_length=40)
        arcpy.management.AddField(points, "DISPLAY_TEXT", "TEXT", field_length=100)
        with arcpy.da.InsertCursor(points, ["SHAPE@XY", "GROUP_NAME", "DISPLAY_TEXT"]) as cursor:
            for xy in [(-80, 35), (10, 65), (90, 20)]:
                cursor.insertRow([xy, "Alpha", "Shared Alpha label"])
            for xy in [(-30, -60), (60, -80)]:
                cursor.insertRow([xy, "Beta", "Shared Beta label"])

        map_object = project.createMap("Multiple Leaders Demo", "MAP")
        map_object.spatialReference = arcpy.SpatialReference(3857)
        # Default basemap is unnecessary for invented local demonstration coordinates.
        for layer in list(map_object.listLayers()):
            if layer.isBasemapLayer:
                map_object.removeLayer(layer)
        layer = map_object.addDataFromPath(points)
        arcpy.management.SelectLayerByAttribute(layer, "NEW_SELECTION", "GROUP_NAME = 'Alpha'")
        map_object.openView()
        view = project.activeView
        if isinstance(view, arcpy.mp.MapView):
            extent = view.getLayerExtent(layer, False, True)
            view.camera.setExtent(extent)
            view.camera.scale *= 1.6

    print(f"Created synthetic demo data: {points}")
    print("Three Alpha points are selected. Use Multiple Leaders > Place Label, then click a label position.")
    print("The project has not been saved automatically.")


if __name__ == "__main__":
    main()
