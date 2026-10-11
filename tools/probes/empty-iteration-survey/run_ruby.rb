# Ruby (Onigmo) battery runner.
File.foreach(File.join(__dir__, "battery.tsv")) do |line|
  next if line.start_with?("#") || line.strip.empty?
  id, pat, subj = line.chomp.split("\t", -1)
  m = Regexp.new(pat).match(subj)
  r = m.nil? ? "nomatch" : "span=#{m.begin(0)},#{m.end(0)} g1=#{m[1].nil? ? 'unset' : "'#{m[1]}'"}"
  puts "ruby #{RUBY_VERSION} (Onigmo)\t#{id}\t#{r}"
end
