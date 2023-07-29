SELECT r."course_id",SUM(c."price") "CourseSUM"
  FROM "courses" c,"register_course" r
  WHERE c."id"=r."course_id"
  GROUP BY r."course_id"
  HAVING SUM(c."price") > 70
  ORDER BY SUM(c.price),r."course_id";
