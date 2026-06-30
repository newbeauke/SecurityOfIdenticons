count = 0
rects = []
for r1 in range(5):
  for r2 in range(r1+1, 5):
    for c1 in range(3):
      for c2 in range(c1+1, 3):
        i1 = 3*r1+c1+1
        i2 = 3*r1+c2+1
        i3 = 3*r2+c1+1
        i4 = 3*r2+c2+1
        if (i1 ^ i2 ^ i3 ^ i4) == 0:
          count += 1
          rects.append(f'Rows {r1},{r2} and Cols {c1},{c2}')
print(f'Count: {count}')
for r in rects:
  print(r)
