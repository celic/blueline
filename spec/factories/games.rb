# == Schema Information
#
# Table name: games
#
#  id      :integer          not null, primary key
#  home_id :integer
#  away_id :integer
#  date    :date
#
# Indexes
#
#  index_games_on_away_id  (away_id)
#  index_games_on_home_id  (home_id)
#

FactoryGirl.define do
  factory :game do
    
  end

end
